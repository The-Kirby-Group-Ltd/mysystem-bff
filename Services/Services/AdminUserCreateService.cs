using Dapper;
using MySqlConnector;
using mysystem_bff.Models.Admin;
using mysystem_bff.Services.Interfaces;

namespace mysystem_bff.Services.Services;

public class AdminUserCreateService : IAdminUserCreateService
{
    private readonly MySqlConnection _db;
    private readonly IAdminUserReadService _readService;

    public AdminUserCreateService(
        MySqlConnection db,
        IAdminUserReadService readService)
    {
        _db = db;
        _readService = readService;
    }

    public async Task<ServiceResult<UserListItemDto>> CreateUser(
        CreateUserRequest request,
        string actingUserId,
        IReadOnlyCollection<string> actingRoles)
    {
        if (string.IsNullOrWhiteSpace(actingUserId))
        {
            return ServiceResult<UserListItemDto>.Fail(
                "Unable to resolve the current user.",
                401);
        }

        var validationError =
            ValidateCreateUserRequest(request);

        if (validationError is not null)
        {
            return ServiceResult<UserListItemDto>.Fail(
                validationError,
                400);
        }

        var permissionError =
            ValidateCreatePermission(
                actingRoles,
                request.Role);

        if (permissionError is not null)
        {
            return ServiceResult<UserListItemDto>.Fail(
                permissionError,
                403);
        }

        var roleId = await _db.QuerySingleOrDefaultAsync<int?>(
            """
            SELECT role_id
            FROM roles
            WHERE role_name = @Role
            LIMIT 1;
            """,
            new { request.Role }
        );

        if (roleId is null)
            return ServiceResult<UserListItemDto>.Fail("Invalid role.", 400);

        var existingUser = await _db.QuerySingleOrDefaultAsync<string>(
            """
            SELECT CAST(user_id AS CHAR)
            FROM users
            WHERE username = @Username OR email = @Email
            LIMIT 1;
            """,
            new
            {
                request.Username,
                request.Email
            }
        );

        if (existingUser is not null)
        {
            return ServiceResult<UserListItemDto>.Fail(
                "A user with this username or email already exists.",
                409
            );
        }

        var userId = Guid.NewGuid().ToString();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);

        await _db.OpenAsync();
        await using var transaction = await _db.BeginTransactionAsync();

        try
        {
            await _db.ExecuteAsync(
                """
                INSERT INTO users (
                    user_id,
                    username,
                    email,
                    password_hash,
                    first_name,
                    last_name,
                    telephone,
                    is_active
                )
                VALUES (
                    @UserId,
                    @Username,
                    @Email,
                    @PasswordHash,
                    @FirstName,
                    @LastName,
                    @Telephone,
                    TRUE
                );
                """,
                new
                {
                    UserId = userId,
                    request.Username,
                    request.Email,
                    PasswordHash = passwordHash,
                    request.FirstName,
                    request.LastName,
                    request.Telephone
                },
                transaction
            );

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
                transaction
            );

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
                    transaction
                );
            }

            foreach (var customerNo in request.CustomerNos
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct())
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
                    transaction
                );
            }

            foreach (var siteId in request.SiteIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct())
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
                    transaction
                );
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        var createdUser = await _readService.GetUserById(userId);

        if (createdUser is null)
            return ServiceResult<UserListItemDto>.Fail("User was created but could not be loaded.", 500);

        return ServiceResult<UserListItemDto>.Created(createdUser);
    }

    private static string? ValidateCreateUserRequest(CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            return "Username is required.";

        if (string.IsNullOrWhiteSpace(request.Email))
            return "Email is required.";

        if (string.IsNullOrWhiteSpace(request.Password))
            return "Password is required.";

        if (string.IsNullOrWhiteSpace(request.FirstName))
            return "First name is required.";

        if (string.IsNullOrWhiteSpace(request.LastName))
            return "Last name is required.";

        if (string.IsNullOrWhiteSpace(request.Role))
            return "Role is required.";

        return null;
    }

    private static string? ValidateCreatePermission(
        IReadOnlyCollection<string> actingRoles,
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

        if (isAdministrator)
        {
            return null;
        }

        if (!isStaff)
        {
            return
                "You do not have permission to create portal users.";
        }

        var staffAllowedRoles =
            new[]
            {
                "Engineer",
                "CustomerUser",
                "SiteUser"
            };

        var roleAllowed =
            staffAllowedRoles.Contains(
                requestedRole,
                StringComparer.OrdinalIgnoreCase);

        if (!roleAllowed)
        {
            return
                "Staff users can only create Engineer, Customer User or Site User accounts.";
        }

        return null;
    }
}