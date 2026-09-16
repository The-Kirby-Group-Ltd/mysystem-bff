namespace mysystem_bff.Controllers.GenericData;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using mysystem_bff.Models.Portal.Queries;
using mysystem_bff.Models.Portal.Responses;
using mysystem_bff.Services.Interfaces.GenericData;
using mysystem_bff.Services.Interfaces.Security;

[ApiController]
[Route("api/portal/calls")]
[Authorize]
public class PortalCallsController : ControllerBase
{
    private readonly IMiddlewareCallsService _callsService;
    private readonly IPortalAccessService _accessService;

    public PortalCallsController(IMiddlewareCallsService callsService, IPortalAccessService accessService)
    {
        _callsService = callsService;
        _accessService = accessService;
    }

    [HttpGet]
    public async Task<ActionResult<PortalCallsResponse>> GetCalls(
    [FromQuery] PortalCallsQuery query,
    CancellationToken ct)
    {
        var customerNo =
            query.CustomerNo?.Trim().ToUpperInvariant() ?? "";

        var siteId =
            query.SiteId?.Trim().ToUpperInvariant() ?? "";

        var callNumber = query.CallNumber;

        // call number lookup must be retrieved first so its site id can be checked
        if (callNumber > 0)
        {
            var callResult = await _callsService.GetCalls(query, ct);

            if (!callResult.Success)
            {
                return StatusCode(
                    callResult.StatusCode,
                    callResult.Error);
            }

            var returnedCall = callResult.Data?.Items.FirstOrDefault();

            if (returnedCall is null)
            {
                return NotFound($"Call {callNumber} was not found.");
            }

            if (!_accessService.HasUnrestrictedAccess(User))
            {
                var canAccessSite = await _accessService.CanAccessSite(
                    User,
                    returnedCall.SiteId,
                    ct);

                if (!canAccessSite)
                    return Forbid();
            }

            return Ok(callResult.Data);
        }

        // Normal customer/site searches
        if (!_accessService.HasUnrestrictedAccess(User))
        {
            if (!string.IsNullOrWhiteSpace(siteId))
            {
                var canAccessSite = await _accessService.CanAccessSite(
                    User,
                    siteId,
                    ct);

                if (!canAccessSite)
                    return Forbid();
            }
            else if (!string.IsNullOrWhiteSpace(customerNo))
            {
                var canAccessCustomer =
                    await _accessService.CanAccessCustomer(
                        User,
                        customerNo,
                        ct);

                if (!canAccessCustomer)
                    return Forbid();
            }
            else
            {
                return BadRequest(
                    "Customer No, Site ID or Call Number is required.");
            }
        }

        var result = await _callsService.GetCalls(query, ct);

        if (!result.Success)
        {
            return StatusCode(
                result.StatusCode,
                result.Error);
        }

        return Ok(result.Data);
    }
}