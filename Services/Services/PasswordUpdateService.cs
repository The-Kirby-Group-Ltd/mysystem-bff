using System.Security.Cryptography;
using System.Text;
using Dapper;
using MySqlConnector;
using mysystem_bff.Models.Admin;
using mysystem_bff.Models.Auth;
using mysystem_bff.Services.Interfaces;

namespace mysystem_bff.Services.Services
{
    public class PasswordUpdateService : IPasswordUpdateService
    {
        private readonly MySqlConnection _db;
        private readonly IConfiguration _configuration;
        private readonly IPortalEmailService _emailService;

        public PasswordUpdateService(
            MySqlConnection db,
            IConfiguration configuration,
            IPortalEmailService emailService)
        {
            _db = db;
            _configuration = configuration;
            _emailService = emailService;
        }

        // =====================================================
        // Send verification code
        // =====================================================

        public async Task<ServiceResult<bool>> SendVerificationCodeAsync(
            string userId,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return ServiceResult<bool>.Fail(
                    "Unable to resolve the current user.",
                    401);
            }

            ct.ThrowIfCancellationRequested();

            // =================================================
            // Load current user
            // =================================================

            var user = await _db.QuerySingleOrDefaultAsync<PasswordUserRow>(
                """
                SELECT
                    CAST(user_id AS CHAR) AS UserId,
                    email AS Email,
                    first_name AS FirstName,
                    is_active AS IsActive
                FROM users
                WHERE CAST(user_id AS CHAR) = @UserId
                LIMIT 1;
                """,
                new
                {
                    UserId = userId
                });

            if (user is null)
            {
                return ServiceResult<bool>.Fail(
                    "User account could not be found.",
                    404);
            }

            if (!user.IsActive)
            {
                return ServiceResult<bool>.Fail(
                    "User account is inactive.",
                    403);
            }

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                return ServiceResult<bool>.Fail(
                    "No email address is registered against this account.",
                    400);
            }

            // =================================================
            // Configuration
            // =================================================

            var codeLifetimeMinutes =
                GetPositiveIntSetting(
                    "PasswordChange:CodeLifetimeMinutes",
                    10);

            var sendCooldownSeconds =
                GetPositiveIntSetting(
                    "PasswordChange:SendCooldownSeconds",
                    60);

            var maximumAttempts =
                GetPositiveIntSetting(
                    "PasswordChange:MaximumAttempts",
                    5);

            // =================================================
            // Cooldown
            // =================================================

            var latestChallengeCreated =
                await _db.QuerySingleOrDefaultAsync<DateTime?>(
                    """
                    SELECT created_at_utc
                    FROM password_change_challenges
                    WHERE user_id = @UserId
                    ORDER BY challenge_id DESC
                    LIMIT 1;
                    """,
                    new
                    {
                        UserId = userId
                    });

            if (
                latestChallengeCreated.HasValue &&
                latestChallengeCreated.Value
                    .AddSeconds(sendCooldownSeconds) > DateTime.UtcNow
            )
            {
                return ServiceResult<bool>.Fail(
                    $"Please wait {sendCooldownSeconds} seconds before requesting another verification code.",
                    429);
            }

            // =================================================
            // Generate code
            // =================================================

            var verificationCode =
                RandomNumberGenerator
                    .GetInt32(0, 1_000_000)
                    .ToString("D6");

            var codeHash =
                HashVerificationCode(
                    userId,
                    verificationCode);

            var now =
                DateTime.UtcNow;

            var expiresAt =
                now.AddMinutes(
                    codeLifetimeMinutes);

            // =================================================
            // Store challenge
            // =================================================

            await using var transaction =
                await _db.BeginTransactionAsync(ct);

            try
            {
                /*
                 * Only one active password-change challenge should
                 * exist for a user at any given time.
                 */
                await _db.ExecuteAsync(
                    """
                    UPDATE password_change_challenges
                    SET consumed_at_utc = @ConsumedAtUtc
                    WHERE user_id = @UserId
                      AND consumed_at_utc IS NULL;
                    """,
                    new
                    {
                        UserId = userId,
                        ConsumedAtUtc = now
                    },
                    transaction);

                await _db.ExecuteAsync(
                    """
                    INSERT INTO password_change_challenges
                    (
                        user_id,
                        code_hash,
                        created_at_utc,
                        expires_at_utc,
                        attempts_remaining,
                        consumed_at_utc
                    )
                    VALUES
                    (
                        @UserId,
                        @CodeHash,
                        @CreatedAtUtc,
                        @ExpiresAtUtc,
                        @AttemptsRemaining,
                        NULL
                    );
                    """,
                    new
                    {
                        UserId = userId,
                        CodeHash = codeHash,
                        CreatedAtUtc = now,
                        ExpiresAtUtc = expiresAt,
                        AttemptsRemaining = maximumAttempts
                    },
                    transaction);

                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }

            // =================================================
            // Send email
            // =================================================

            try
            {
                await _emailService
                    .SendPasswordVerificationCodeAsync(
                        user.Email,
                        user.FirstName,
                        verificationCode,
                        ct);
            }
            catch
            {
                /*
                 * If sending fails, invalidate the challenge so a
                 * code which the user never received cannot remain live.
                 */
                await _db.ExecuteAsync(
                    """
                    UPDATE password_change_challenges
                    SET consumed_at_utc = @ConsumedAtUtc
                    WHERE user_id = @UserId
                      AND consumed_at_utc IS NULL;
                    """,
                    new
                    {
                        UserId = userId,
                        ConsumedAtUtc = DateTime.UtcNow
                    });

                return ServiceResult<bool>.Fail(
                    "Unable to send the verification email.",
                    502);
            }

            return ServiceResult<bool>.Ok(true);
        }

        // =====================================================
        // Change password
        // =====================================================

        public async Task<ServiceResult<bool>> ChangePasswordAsync(
            string userId,
            ChangePasswordRequest request,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return ServiceResult<bool>.Fail(
                    "Unable to resolve the current user.",
                    401);
            }

            if (request is null)
            {
                return ServiceResult<bool>.Fail(
                    "Password change request is required.",
                    400);
            }

            var oldPassword =
                request.OldPassword?.Trim() ?? "";

            var newPassword =
                request.NewPassword ?? "";

            var verificationCode =
                request.VerificationCode?.Trim() ?? "";

            if (
                string.IsNullOrWhiteSpace(oldPassword) ||
                string.IsNullOrWhiteSpace(newPassword) ||
                string.IsNullOrWhiteSpace(verificationCode)
            )
            {
                return ServiceResult<bool>.Fail(
                    "Old password, new password and verification code are required.",
                    400);
            }

            if (
                verificationCode.Length != 6 ||
                !verificationCode.All(char.IsDigit)
            )
            {
                return ServiceResult<bool>.Fail(
                    "Verification code must contain exactly six digits.",
                    400);
            }

            /*
             * Keep this aligned with whatever password policy you
             * ultimately decide to enforce everywhere.
             */
            if (newPassword.Length < 8)
            {
                return ServiceResult<bool>.Fail(
                    "New password must be at least 8 characters long.",
                    400);
            }

            ct.ThrowIfCancellationRequested();

            // =================================================
            // Load current user
            // =================================================

            var user =
                await _db.QuerySingleOrDefaultAsync<PasswordUserRow>(
                    """
                    SELECT
                        CAST(user_id AS CHAR) AS UserId,
                        password_hash AS PasswordHash,
                        is_active AS IsActive
                    FROM users
                    WHERE CAST(user_id AS CHAR) = @UserId
                    LIMIT 1;
                    """,
                    new
                    {
                        UserId = userId
                    });

            if (user is null)
            {
                return ServiceResult<bool>.Fail(
                    "User account could not be found.",
                    404);
            }

            if (!user.IsActive)
            {
                return ServiceResult<bool>.Fail(
                    "User account is inactive.",
                    403);
            }

            // =================================================
            // Verify existing password
            // =================================================

            var oldPasswordValid =
                BCrypt.Net.BCrypt.Verify(
                    oldPassword,
                    user.PasswordHash);

            if (!oldPasswordValid)
            {
                return ServiceResult<bool>.Fail(
                    "Current password is incorrect.",
                    400);
            }

            /*
             * Prevent changing to the same password.
             */
            var newPasswordMatchesExisting =
                BCrypt.Net.BCrypt.Verify(
                    newPassword,
                    user.PasswordHash);

            if (newPasswordMatchesExisting)
            {
                return ServiceResult<bool>.Fail(
                    "New password must be different from the current password.",
                    400);
            }

            // =================================================
            // Load active challenge
            // =================================================

            var challenge =
                await _db.QuerySingleOrDefaultAsync<PasswordChallengeRow>(
                    """
                    SELECT
                        challenge_id AS ChallengeId,
                        code_hash AS CodeHash,
                        expires_at_utc AS ExpiresAtUtc,
                        attempts_remaining AS AttemptsRemaining
                    FROM password_change_challenges
                    WHERE user_id = @UserId
                      AND consumed_at_utc IS NULL
                    ORDER BY challenge_id DESC
                    LIMIT 1;
                    """,
                    new
                    {
                        UserId = userId
                    });

            if (challenge is null)
            {
                return ServiceResult<bool>.Fail(
                    "No active password verification code exists. Please request a new code.",
                    400);
            }

            // =================================================
            // Challenge expiry / attempts
            // =================================================

            if (challenge.ExpiresAtUtc <= DateTime.UtcNow)
            {
                await ConsumeChallenge(
                    challenge.ChallengeId);

                return ServiceResult<bool>.Fail(
                    "The verification code has expired. Please request a new code.",
                    400);
            }

            if (challenge.AttemptsRemaining <= 0)
            {
                await ConsumeChallenge(
                    challenge.ChallengeId);

                return ServiceResult<bool>.Fail(
                    "The verification code can no longer be used. Please request a new code.",
                    429);
            }

            // =================================================
            // Verify code
            // =================================================

            var submittedHash =
                HashVerificationCode(
                    userId,
                    verificationCode);

            var codeValid =
                FixedTimeHashEquals(
                    challenge.CodeHash,
                    submittedHash);

            if (!codeValid)
            {
                var attemptsRemaining =
                    challenge.AttemptsRemaining - 1;

                await _db.ExecuteAsync(
                    """
                    UPDATE password_change_challenges
                    SET
                        attempts_remaining = @AttemptsRemaining,
                        consumed_at_utc =
                            CASE
                                WHEN @AttemptsRemaining <= 0
                                THEN @ConsumedAtUtc
                                ELSE consumed_at_utc
                            END
                    WHERE challenge_id = @ChallengeId;
                    """,
                    new
                    {
                        challenge.ChallengeId,
                        AttemptsRemaining = attemptsRemaining,
                        ConsumedAtUtc = DateTime.UtcNow
                    });

                return ServiceResult<bool>.Fail(
                    attemptsRemaining > 0
                        ? $"Verification code is incorrect. {attemptsRemaining} attempt(s) remaining."
                        : "Verification code is incorrect and can no longer be used.",
                    400);
            }

            // =================================================
            // Update password + consume challenge
            // =================================================

            var newPasswordHash =
                BCrypt.Net.BCrypt.HashPassword(
                    newPassword);

            await using var transaction =
                await _db.BeginTransactionAsync(ct);

            try
            {
                var updatedRows =
                    await _db.ExecuteAsync(
                        """
                        UPDATE users
                        SET password_hash = @PasswordHash
                        WHERE CAST(user_id AS CHAR) = @UserId
                          AND is_active = 1;
                        """,
                        new
                        {
                            UserId = userId,
                            PasswordHash = newPasswordHash
                        },
                        transaction);

                if (updatedRows != 1)
                {
                    await transaction.RollbackAsync(ct);

                    return ServiceResult<bool>.Fail(
                        "Unable to update the password.",
                        500);
                }

                await _db.ExecuteAsync(
                    """
                    UPDATE password_change_challenges
                    SET consumed_at_utc = @ConsumedAtUtc
                    WHERE challenge_id = @ChallengeId;
                    """,
                    new
                    {
                        ChallengeId = challenge.ChallengeId,
                        ConsumedAtUtc = DateTime.UtcNow
                    },
                    transaction);

                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }

            return ServiceResult<bool>.Ok(true);
        }

        // =====================================================
        // Verification hashing
        // =====================================================

        private string HashVerificationCode(
            string userId,
            string verificationCode)
        {
            var secret =
                _configuration["PasswordChange:CodeHashSecret"];

            if (string.IsNullOrWhiteSpace(secret))
            {
                throw new InvalidOperationException(
                    "PasswordChange CodeHashSecret is missing.");
            }

            var key =
                Encoding.UTF8.GetBytes(secret);

            var value =
                Encoding.UTF8.GetBytes(
                    $"{userId}|{verificationCode}");

            using var hmac =
                new HMACSHA256(key);

            return Convert.ToHexString(
                hmac.ComputeHash(value));
        }

        // =====================================================
        // Fixed-time hash comparison
        // =====================================================

        private static bool FixedTimeHashEquals(
            string expectedHash,
            string suppliedHash)
        {
            try
            {
                var expectedBytes =
                    Convert.FromHexString(expectedHash);

                var suppliedBytes =
                    Convert.FromHexString(suppliedHash);

                return CryptographicOperations.FixedTimeEquals(
                    expectedBytes,
                    suppliedBytes);
            }
            catch
            {
                return false;
            }
        }

        // =====================================================
        // Consume challenge
        // =====================================================

        private async Task ConsumeChallenge(
            long challengeId)
        {
            await _db.ExecuteAsync(
                """
                UPDATE password_change_challenges
                SET consumed_at_utc = @ConsumedAtUtc
                WHERE challenge_id = @ChallengeId;
                """,
                new
                {
                    ChallengeId = challengeId,
                    ConsumedAtUtc = DateTime.UtcNow
                });
        }

        // =====================================================
        // Configuration helper
        // =====================================================

        private int GetPositiveIntSetting(
            string key,
            int defaultValue)
        {
            var value =
                _configuration[key];

            if (
                int.TryParse(value, out var parsed) &&
                parsed > 0
            )
            {
                return parsed;
            }

            return defaultValue;
        }

        // =====================================================
        // Internal database rows
        // =====================================================

        private class PasswordUserRow
        {
            public string UserId { get; set; } = "";

            public string Email { get; set; } = "";

            public string FirstName { get; set; } = "";

            public string PasswordHash { get; set; } = "";

            public bool IsActive { get; set; }
        }

        private class PasswordChallengeRow
        {
            public long ChallengeId { get; set; }

            public string CodeHash { get; set; } = "";

            public DateTime ExpiresAtUtc { get; set; }

            public int AttemptsRemaining { get; set; }
        }
    }
}