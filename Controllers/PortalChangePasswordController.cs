namespace mysystem_bff.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using mysystem_bff.Models.Auth;
using mysystem_bff.Services.Interfaces;
using System.Security.Claims;

[ApiController]
[Route("api/change-password")]
[Authorize]
public class PortalChangePasswordController : ControllerBase
{
    private readonly IPasswordUpdateService _passwordService;

    public PortalChangePasswordController(
        IPasswordUpdateService passwordService)
    {
        _passwordService = passwordService;
    }

    // ========================================================
    // Send 2FA code to email 
    // ========================================================

    [HttpPost]
    [Route("code")]
    public async Task<ActionResult> RequestPasswordChangeMFACode(
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized("Unable to resolve the current user.");

        var result = await
            _passwordService.SendVerificationCodeAsync(
                userId,
                ct);

        if (!result.Success)
            return StatusCode(result.StatusCode, result.Error);

        return Ok(new
        {
            message = "Verification code was sent to your registered email address."
        });
    }

    // ========================================================
    // Change password
    // ========================================================

    [HttpPost]
    public async Task<ActionResult> ChangeUserPassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized("Unable to resolve the current user.");

        var result = await _passwordService.ChangePasswordAsync(
            userId,
            request,
            ct);

        if (!result.Success)
            return StatusCode(result.StatusCode, result.Error);

        return Ok(new
        {
            message = "Password changed successfully."
        });
    }
}