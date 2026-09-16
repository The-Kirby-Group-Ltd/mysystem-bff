using Dapper;
using MySqlConnector;
using mysystem_bff.Models.Admin;
using mysystem_bff.Services.Interfaces.Admin;

namespace mysystem_bff.Services.Services.Admin;

public class AdminUserUpdateService : IAdminUserUpdateService
{
    private readonly MySqlConnection _db;
    private readonly IAdminUserReadService _readService;

    public AdminUserUpdateService(
        MySqlConnection db,
        IAdminUserReadService readService)
    {
        _db = db;
        _readService = readService;
    }

    // =========================================================
    // Update user
    // =========================================================

    public async Task<ServiceResult<UserListItemDto>> UpdateUser(
        string userId,
        UpdateUserRequest request,
        string actingUserId,
        IReadOnlyCollection<string> actingRoles)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return ServiceResult<UserListItemDto>.Fail(
                "User ID is required.",
                400);
        }

        if (string.IsNullOrWhiteSpace(actingUserId))
        {
            return ServiceResult<UserListItemDto>.Fail(
                "Unable to resolve the current user.",
                401);
        }

        var validationError =
            ValidateUpdateUserRequest(request);

        if (validationError is not null)
        {
            return ServiceResult<UserListItemDto>.Fail(
                validationError,
                400);
        }

        // =====================================================
        // Resolve target user and current role
        // =====================================================

        var targetUser =
            await _db.QuerySingleOrDefaultAsync<TargetUserRow>(
                """
                SELECT
                    CAST(u.user_id AS CHAR) AS UserId,
                    r.role_name AS Role
                FROM users u
                LEFT JOIN user_roles ur
                    ON ur.user_id = u.user_id
                LEFT JOIN roles r
                    ON r.role_id = ur.role_id
                WHERE CAST(u.user_id AS CHAR) = @UserId
                LIMIT 1;
                """,
                new
                {
                    UserId = userId
                });

        if (targetUser is null)
        {
            return ServiceResult<UserListItemDto>.Fail(
                "User not found.",
                404);
        }

        // =====================================================
        // Permission checks
        // =====================================================

        var permissionError =
            ValidateUpdatePermission(
                actingUserId,
                actingRoles,
                targetUser,
                request.Role);

        if (permissionError is not null)
        {
            return ServiceResult<UserListItemDto>.Fail(
                permissionError,
                403);
        }

        // =====================================================
        // Duplicate username / email check
        // =====================================================

        var duplicateUser =
            await _db.QuerySingleOrDefaultAsync<string>(
                """
                SELECT CAST(user_id AS CHAR)
                FROM users
                WHERE
                    (username = @Username OR email = @Email)
                    AND CAST(user_id AS CHAR) <> @UserId
                LIMIT 1;
                """,
                new
                {
                    UserId = userId,
                    request.Username,
                    request.Email
                });

        if (duplicateUser is not null)
        {
            return ServiceResult<UserListItemDto>.Fail(
                "Another user already has this username or email.",
                409);
        }

        // =====================================================
        // Resolve requested role
        // =====================================================

        var roleId =
            await _db.QuerySingleOrDefaultAsync<int?>(
                """
                SELECT role_id
                FROM roles
                WHERE role_name = @Role
                LIMIT 1;
                """,
                new
                {
                    request.Role
                });

        if (roleId is null)
        {
            return ServiceResult<UserListItemDto>.Fail(
                "Invalid role.",
                400);
        }

        // =====================================================
        // Begin update transaction
        // =====================================================

        if (_db.State != System.Data.ConnectionState.Open)
        {
            await _db.OpenAsync();
        }

        await using var transaction =
            await _db.BeginTransactionAsync();

        try
        {
            // =================================================
            // Update core user details
            // =================================================

            await _db.ExecuteAsync(
                """
                UPDATE users
                SET
                    username = @Username,
                    email = @Email,
                    first_name = @FirstName,
                    last_name = @LastName,
                    telephone = @Telephone,
                    updated_at = CURRENT_TIMESTAMP
                WHERE CAST(user_id AS CHAR) = @UserId;
                """,
                new
                {
                    UserId = userId,
                    request.Username,
                    request.Email,
                    request.FirstName,
                    request.LastName,
                    request.Telephone
                },
                transaction);

            // =================================================
            // Update role
            // =================================================

            await _db.ExecuteAsync(
                """
                DELETE FROM user_roles
                WHERE CAST(user_id AS CHAR) = @UserId;
                """,
                new
                {
                    UserId = userId
                },
                transaction);

            await _db.ExecuteAsync(
                """
                INSERT INTO user_roles (
                    user_id,
                    role_id
                )
                VALUES (
                    @UserId,
                    @RoleId
                );
                """,
                new
                {
                    UserId = userId,
                    RoleId = roleId.Value
                },
                transaction);

            // =================================================
            // Update staff profile
            // =================================================

            await _db.ExecuteAsync(
                """
                DELETE FROM staff_profiles
                WHERE CAST(user_id AS CHAR) = @UserId;
                """,
                new
                {
                    UserId = userId
                },
                transaction);

            if (!string.IsNullOrWhiteSpace(request.Position))
            {
                await _db.ExecuteAsync(
                    """
                    INSERT INTO staff_profiles (
                        user_id,
                        position
                    )
                    VALUES (
                        @UserId,
                        @Position
                    );
                    """,
                    new
                    {
                        UserId = userId,
                        request.Position
                    },
                    transaction);
            }

            // =================================================
            // Update customer access
            // =================================================

            await _db.ExecuteAsync(
                """
                DELETE FROM user_customer_access
                WHERE CAST(user_id AS CHAR) = @UserId;
                """,
                new
                {
                    UserId = userId
                },
                transaction);

            foreach (
                var customerNo in request.CustomerNos
                    .Where(x =>
                        !string.IsNullOrWhiteSpace(x))
                    .Select(x =>
                        x.Trim().ToUpperInvariant())
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase))
            {
                await _db.ExecuteAsync(
                    """
                    INSERT INTO user_customer_access (
                        user_id,
                        customer_no
                    )
                    VALUES (
                        @UserId,
                        @CustomerNo
                    );
                    """,
                    new
                    {
                        UserId = userId,
                        CustomerNo = customerNo
                    },
                    transaction);
            }

            // =================================================
            // Update site access
            // =================================================

            await _db.ExecuteAsync(
                """
                DELETE FROM user_site_access
                WHERE CAST(user_id AS CHAR) = @UserId;
                """,
                new
                {
                    UserId = userId
                },
                transaction);

            foreach (
                var siteId in request.SiteIds
                    .Where(x =>
                        !string.IsNullOrWhiteSpace(x))
                    .Select(x =>
                        x.Trim().ToUpperInvariant())
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase))
            {
                await _db.ExecuteAsync(
                    """
                    INSERT INTO user_site_access (
                        user_id,
                        site_id
                    )
                    VALUES (
                        @UserId,
                        @SiteId
                    );
                    """,
                    new
                    {
                        UserId = userId,
                        SiteId = siteId
                    },
                    transaction);
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        // =====================================================
        // Reload updated user
        // =====================================================

        var updatedUser =
            await _readService.GetUserById(
                userId);

        if (updatedUser is null)
        {
            return ServiceResult<UserListItemDto>.Fail(
                "User was updated but could not be loaded.",
                500);
        }

        return ServiceResult<UserListItemDto>.Ok(
            updatedUser);
    }

    // =========================================================
    // Request validation
    // =========================================================

    private static string? ValidateUpdateUserRequest(
        UpdateUserRequest request)
    {
        if (request is null)
        {
            return "Update request is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return "Username is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return "Email is required.";
        }

        if (string.IsNullOrWhiteSpace(request.FirstName))
        {
            return "First name is required.";
        }

        if (string.IsNullOrWhiteSpace(request.LastName))
        {
            return "Last name is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Role))
        {
            return "Role is required.";
        }

        return null;
    }

    // =========================================================
    // Administration permission validation
    // =========================================================

    private static string? ValidateUpdatePermission(
        string actingUserId,
        IReadOnlyCollection<string> actingRoles,
        TargetUserRow targetUser,
        string requestedRole)
    {
        var isAdministrator =
            actingRoles.Contains(
                "Administrator",
                StringComparer.OrdinalIgnoreCase);

        var isStaff =
            actingRoles.Contains(
                "Staff",
                StringComparer.OrdinalIgnoreCase);

        // Administrators may update any user.
        if (isAdministrator)
        {
            return null;
        }

        // Only Staff or Administrator should reach this service.
        if (!isStaff)
        {
            return
                "You do not have permission to update portal users.";
        }

        // Staff cannot modify Administrator or Staff accounts.
        if (IsPrivilegedRole(targetUser.Role))
        {
            return
                "Staff users cannot modify Administrator or Staff accounts.";
        }

        // Staff cannot promote another user to Staff or Administrator.
        if (IsPrivilegedRole(requestedRole))
        {
            return
                "Staff users cannot assign the Administrator or Staff role.";
        }

        return null;
    }

    // =========================================================
    // Role helpers
    // =========================================================

    private static bool IsPrivilegedRole(
        string? role)
    {
        return
            string.Equals(
                role,
                "Administrator",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                role,
                "Staff",
                StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================
    // Internal database rows
    // =========================================================

    private class TargetUserRow
    {
        public string UserId { get; set; } = "";

        public string Role { get; set; } = "";
    }
}