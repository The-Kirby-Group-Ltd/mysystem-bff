using Dapper;
using MySqlConnector;
using mysystem_bff.Models.Admin;
using mysystem_bff.Services.Interfaces.Admin;

namespace mysystem_bff.Services.Services.Admin;

public class AdminRoleService : IAdminRoleService
{
    private readonly MySqlConnection _db;

    public AdminRoleService(MySqlConnection db)
    {
        _db = db;
    }

    public async Task<List<RoleDto>> GetRoles()
    {
        var roles = await _db.QueryAsync<RoleDto>(
            """
            SELECT
                role_id AS RoleId,
                role_name AS RoleName
            FROM roles
            ORDER BY role_id ASC;
            """
        );

        return roles.ToList();
    }
}