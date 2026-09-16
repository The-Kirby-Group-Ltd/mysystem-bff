namespace mysystem_bff.Controllers.Admin;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using mysystem_bff.Models.Admin;
using mysystem_bff.Services.Interfaces.Admin;

[ApiController]
[Route("api/admin/roles")]
[Authorize(Policy = "AdministratorOnly")]
public class AdminRolesController : ControllerBase
{
    private readonly IAdminRoleService _roleService;

    public AdminRolesController(IAdminRoleService roleService)
    {
        _roleService = roleService;
    }

    [HttpGet]
    public async Task<ActionResult<List<RoleDto>>> GetRoles()
    {
        var roles = await _roleService.GetRoles();
        return Ok(roles);
    }
}