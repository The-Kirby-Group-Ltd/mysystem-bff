using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using mysystem_bff.Models.Portal.Queries;
using mysystem_bff.Models.Portal.Responses;
using mysystem_bff.Services.Interfaces.GenericData;
using mysystem_bff.Services.Interfaces.Security;

namespace mysystem_bff.Controllers.GenericData;

[ApiController]
[Route("api/portal/call-actions")]
[Authorize]
public class PortalCallActionsController : ControllerBase
{
    private readonly IMiddlewareCallActionsService _callActionsService;
    private readonly IPortalAccessService _accessService;
    private readonly IMiddlewareCallsService _callsService;

    public PortalCallActionsController(
        IMiddlewareCallActionsService callActionsService,
        IPortalAccessService accessService,
        IMiddlewareCallsService callsService)
    {
        _callActionsService = callActionsService;
        _accessService = accessService;
        _callsService = callsService;
    }

    [HttpGet]
    public async Task<ActionResult<PortalCallActionsResponse>> GetCallActions(
        [FromQuery] PortalCallActionsQuery query,
        CancellationToken ct)
    {
        // Call number is required because every action belongs to a call.
        if (query.CallNumber <= 0)
        {
            return BadRequest(
                "A valid Call Number is required.");
        }

        // Load the parent call first. This gives us the Site ID required
        // to authorise restricted users before exposing any call actions.
        var callResult = await _callsService.GetCalls(
            new PortalCallsQuery
            {
                CallNumber = query.CallNumber,
                Page = 1,
                PageSize = 1
            },
            ct);

        if (!callResult.Success)
        {
            return StatusCode(
                callResult.StatusCode,
                callResult.Error);
        }

        var parentCall =
            callResult.Data?.Items.FirstOrDefault();

        if (parentCall is null)
        {
            return NotFound(
                $"Call {query.CallNumber} was not found.");
        }

        // Kirby staff have unrestricted access. Other users must have
        // access to the site associated with the parent call.
        if (!_accessService.HasUnrestrictedAccess(User))
        {
            var canAccessSite =
                await _accessService.CanAccessSite(
                    User,
                    parentCall.SiteId,
                    ct);

            if (!canAccessSite)
            {
                return Forbid();
            }
        }

        // The user is authorised, so retrieve the actions.
        var result =
            await _callActionsService.GetCallActions(
                query,
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