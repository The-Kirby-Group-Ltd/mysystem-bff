namespace mysystem_bff.Controllers.Security;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using mysystem_bff.Services.Interfaces.Security;

[ApiController]
[Route("api/middleware")]
[Authorize]
public class MiddlewareAuthController : ControllerBase
{
    private readonly IMiddlewareAuthService _middlewareAuthService;

    public MiddlewareAuthController(IMiddlewareAuthService middlewareAuthService)
    {
        _middlewareAuthService = middlewareAuthService;
    }

    [HttpPost("token")]
    public async Task<IActionResult> GetToken()
    {
        var result = await _middlewareAuthService.GetMiddlewareToken();

        if (!result.Success)
            return StatusCode(result.StatusCode, result.Error);

        return Ok(new
        {
            accessToken = result.Data
        });
    }
}