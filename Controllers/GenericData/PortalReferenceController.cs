using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using mysystem_bff.Models.Portal.Reference;
using mysystem_bff.Services.Interfaces.GenericData;

namespace mysystem_bff.Controllers.GenericData;

[ApiController]
[Route("api/portal/reference")]
[Authorize]
public class PortalReferenceController : ControllerBase
{
    private readonly IMiddlewareReferenceService _referenceService;

    public PortalReferenceController(
        IMiddlewareReferenceService referenceService)
    {
        _referenceService = referenceService;
    }

    [HttpGet("system-types")]
    public async Task<IActionResult> GetSystemTypes(
        [FromQuery] PortalReferenceQuery query,
        CancellationToken ct)
    {
        var result = await _referenceService.GetSystemTypes(query, ct);

        if (!result.Success)
            return StatusCode(result.StatusCode, result.Error);

        return Ok(result.Data);
    }

    [HttpGet("engineers")]
    public async Task<IActionResult> GetEngineers(
        [FromQuery] PortalReferenceQuery query,
        CancellationToken ct)
    {
        var result = await _referenceService.GetEngineers(query, ct);

        if (!result.Success)
            return StatusCode(result.StatusCode, result.Error);

        return Ok(result.Data);
    }

    [HttpGet("failed-to-respond-reasons/{code}")]
    public async Task<ActionResult<PortalFailedToRespondReasonDto>>
    GetFailedToRespondReason(
        string code,
        CancellationToken ct)
    {
        var result =
            await _referenceService.GetFailedToRespondReason(
                code,
                ct);

        if (!result.Success)
        {
            return StatusCode(
                result.StatusCode,
                result.Error);
        }

        return Ok(result.Data);
    }
}