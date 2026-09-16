using mysystem_bff.Models.Admin;

namespace mysystem_bff.Services.Interfaces.Admin;

public interface IAdminRoleService
{
    Task<List<RoleDto>> GetRoles();
}