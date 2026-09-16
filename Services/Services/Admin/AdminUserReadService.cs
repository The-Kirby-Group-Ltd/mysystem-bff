using Dapper;
using MySqlConnector;
using mysystem_bff.Models.Admin;
using mysystem_bff.Services.Interfaces.Admin;

namespace mysystem_bff.Services.Services.Admin;

public class AdminUserReadService : IAdminUserReadService
{
    private readonly MySqlConnection _db;

    public AdminUserReadService(MySqlConnection db)
    {
        _db = db;
    }

    public async Task<List<UserListItemDto>> GetUsers(UserFilters filters)
    {
        var sql = """
            SELECT DISTINCT
                CAST(u.user_id AS CHAR) AS UserId,
                u.username AS Username,
                u.email AS Email,
                u.first_name AS FirstName,
                u.last_name AS LastName,
                u.telephone AS Telephone,
                u.is_active AS IsActive,
                u.created_at AS CreatedAt,
                sp.position AS Position
            FROM users u
            LEFT JOIN staff_profiles sp ON sp.user_id = u.user_id
            LEFT JOIN user_roles ur ON ur.user_id = u.user_id
            LEFT JOIN roles r ON r.role_id = ur.role_id
            LEFT JOIN user_customer_access uca ON uca.user_id = u.user_id
            LEFT JOIN user_site_access usa ON usa.user_id = u.user_id
            WHERE 1 = 1
        """;

        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(filters.UserId))
        {
            sql += " AND CAST(u.user_id AS CHAR) = @UserId";
            parameters.Add("UserId", filters.UserId);
        }

        if (!string.IsNullOrWhiteSpace(filters.Role))
        {
            sql += " AND r.role_name = @Role";
            parameters.Add("Role", filters.Role);
        }

        if (!string.IsNullOrWhiteSpace(filters.CustomerNo))
        {
            sql += " AND uca.customer_no = @CustomerNo";
            parameters.Add("CustomerNo", filters.CustomerNo);
        }

        if (!string.IsNullOrWhiteSpace(filters.SiteId))
        {
            sql += " AND usa.site_id = @SiteId";
            parameters.Add("SiteId", filters.SiteId);
        }

        if (filters.IsActive.HasValue)
        {
            sql += " AND u.is_active = @IsActive";
            parameters.Add("IsActive", filters.IsActive.Value);
        }

        sql += " ORDER BY u.created_at DESC;";

        var users = (await _db.QueryAsync<UserListItemDto>(sql, parameters)).ToList();

        foreach (var user in users)
        {
            await PopulateUserExtras(user);
        }

        return users;
    }

    public async Task<UserListItemDto?> GetUserById(string userId)
    {
        var user = await _db.QuerySingleOrDefaultAsync<UserListItemDto>(
            """
            SELECT
                CAST(u.user_id AS CHAR) AS UserId,
                u.username AS Username,
                u.email AS Email,
                u.first_name AS FirstName,
                u.last_name AS LastName,
                u.telephone AS Telephone,
                u.is_active AS IsActive,
                u.created_at AS CreatedAt,
                sp.position AS Position
            FROM users u
            LEFT JOIN staff_profiles sp ON sp.user_id = u.user_id
            WHERE CAST(u.user_id AS CHAR) = @UserId
            LIMIT 1;
            """,
            new { UserId = userId }
        );

        if (user is null)
            return null;

        await PopulateUserExtras(user);

        return user;
    }

    private async Task PopulateUserExtras(UserListItemDto user)
    {
        user.Roles = (await _db.QueryAsync<string>(
            """
            SELECT r.role_name
            FROM roles r
            INNER JOIN user_roles ur ON ur.role_id = r.role_id
            WHERE CAST(ur.user_id AS CHAR) = @UserId
            ORDER BY r.role_name;
            """,
            new { user.UserId }
        )).ToList();

        user.CustomerNos = (await _db.QueryAsync<string>(
            """
            SELECT customer_no
            FROM user_customer_access
            WHERE CAST(user_id AS CHAR) = @UserId
            ORDER BY customer_no;
            """,
            new { user.UserId }
        )).ToList();

        user.SiteIds = (await _db.QueryAsync<string>(
            """
            SELECT site_id
            FROM user_site_access
            WHERE CAST(user_id AS CHAR) = @UserId
            ORDER BY site_id;
            """,
            new { user.UserId }
        )).ToList();
    }
}