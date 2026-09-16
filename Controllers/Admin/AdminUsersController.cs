namespace mysystem_bff.Controllers.Admin;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using mysystem_bff.Models.Admin;
using mysystem_bff.Services.Interfaces.Admin;
using System.Security.Claims;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = "AdministrationAccess")]
public class AdminUsersController : ControllerBase
{
    private readonly IAdminUserReadService _readService;
    private readonly IAdminUserCreateService _createService;
    private readonly IAdminUserUpdateService _updateService;
    private readonly IAdminUserStatusService _statusService;
    private readonly IAdminUserPasswordService _passwordService;

    public AdminUsersController(
        IAdminUserReadService readService,
        IAdminUserCreateService createService,
        IAdminUserUpdateService updateService,
        IAdminUserStatusService statusService,
        IAdminUserPasswordService passwordService)
    {
        _readService = readService;
        _createService = createService;
        _updateService = updateService;
        _statusService = statusService;
        _passwordService = passwordService;
    }

    // get all users
    [HttpGet]
    public async Task<ActionResult<List<UserListItemDto>>> GetUsers([FromQuery] UserFilters filters)
    {
        var users = await _readService.GetUsers(filters);
        return Ok(users);
    }

    // get specific user by id
    [HttpGet("{userId}")]
    public async Task<ActionResult<UserListItemDto>> GetUserById(string userId)
    {
        var user = await _readService.GetUserById(userId);

        if (user is null)
            return NotFound("User not found.");

        return Ok(user);
    }

    // create new user
    [HttpPost]
    public async Task<ActionResult<UserListItemDto>> CreateUser(
        CreateUserRequest request)
    {
        var actingUserId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(actingUserId))
        {
            return Unauthorized(
                "Unable to resolve the current user.");
        }

        var actingRoles =
            User.FindAll(ClaimTypes.Role)
                .Select(role => role.Value)
                .ToList();

        var result =
            await _createService.CreateUser(
                request,
                actingUserId,
                actingRoles);

        if (!result.Success)
        {
            return StatusCode(
                result.StatusCode,
                result.Error);
        }

        return StatusCode(
            result.StatusCode,
            result.Data);
    }

    // update specific user attribute(s)
    [HttpPatch("{userId}")]
    public async Task<ActionResult<UserListItemDto>> UpdateUser(
        string userId,
        UpdateUserRequest request)
    {
        var actingUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(actingUserId))
            return Unauthorized("Unable to resolve the current user");

        var actingRoles = 
            User.FindAll(ClaimTypes.Role)
                .Select(role => role.Value)
                .ToList();
        
        var result = await _updateService.UpdateUser(
            userId,
            request,
            actingUserId,
            actingRoles);

        if (!result.Success)
            return StatusCode(result.StatusCode, result.Error);

        return Ok(result.Data);
    }

    // update user "isActive" status true/false
    [HttpPatch("{userId}/status")]
    public async Task<IActionResult> UpdateUserStatus(
        string userId,
        UpdateUserStatusRequest request)
    {
        var result = await _statusService.UpdateUserStatus(userId, request);

        if (!result.Success)
            return StatusCode(result.StatusCode, result.Error);

        return Ok(result.Data);
    }

    // reset user password 
    [HttpPost("{userId}/reset-password")]
    [EnableRateLimiting("Security")]
    public async Task<IActionResult> ResetPassword(
    string userId,
    ResetPasswordRequest request)
    {
        var result = await _passwordService.ResetPassword(userId, request);

        if (!result.Success)
            return StatusCode(result.StatusCode, result.Error);

        return Ok(result.Data);
    }
}