using Dapper;
using MySqlConnector;
using mysystem_bff.Models.Admin;
using mysystem_bff.Services.Interfaces.Admin;

namespace mysystem_bff.Services.Services.Admin;

public class AdminUserPasswordService : IAdminUserPasswordService
{
    private readonly MySqlConnection _db;

    public AdminUserPasswordService(MySqlConnection db)
    {
        _db = db;
    }

    public async Task<ServiceResult<object>> ResetPassword(
        string userId,
        ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return ServiceResult<object>.Fail("User ID is required.", 400);

        if (string.IsNullOrWhiteSpace(request.NewPassword))
            return ServiceResult<object>.Fail("New password is required.", 400);

        if (request.NewPassword.Length < 8)
            return ServiceResult<object>.Fail("Password must be at least 8 characters.", 400);

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12);

        var affectedRows = await _db.ExecuteAsync(
            """
            UPDATE users
            SET
                password_hash = @PasswordHash,
                updated_at = CURRENT_TIMESTAMP
            WHERE CAST(user_id AS CHAR) = @UserId;
            """,
            new
            {
                UserId = userId,
                PasswordHash = passwordHash
            }
        );

        if (affectedRows == 0)
            return ServiceResult<object>.Fail("User not found.", 404);

        return ServiceResult<object>.Ok(new
        {
            userId,
            passwordUpdated = true
        });
    }
}