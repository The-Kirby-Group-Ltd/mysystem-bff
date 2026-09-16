namespace mysystem_bff.Controllers.GenericData;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using mysystem_bff.Models.Portal.Queries;
using mysystem_bff.Models.Portal.Responses;
using mysystem_bff.Services.Interfaces.GenericData;
using mysystem_bff.Services.Interfaces.Security;

[ApiController]
[Route("api/portal/sites")]
[Authorize]
public class PortalSitesController : ControllerBase
{
    private readonly IMiddlewareSitesService _sitesService;
    private readonly IPortalAccessService _accessService;

    public PortalSitesController(
        IMiddlewareSitesService sitesService, IPortalAccessService accessService)
    {
        _sitesService = sitesService;
        _accessService = accessService;
    }

    [HttpGet]
    public async Task<ActionResult<PortalSitesResponse>> GetSites(
    [FromQuery] PortalSitesQuery query,
    CancellationToken cancellationToken)
    {
        var customerNo = query.CustomerNo?.Trim().ToUpperInvariant() ?? "";
        var siteId = query.SiteId?.Trim().ToUpperInvariant() ?? "";

        if (!_accessService.HasUnrestrictedAccess(User))
        {
            if (!string.IsNullOrWhiteSpace(siteId))
            {
                var canAccessSite = await _accessService.CanAccessSite(
                    User,
                    siteId,
                    cancellationToken);

                if (!canAccessSite)
                    return Forbid();
            }
            else if (!string.IsNullOrWhiteSpace(customerNo))
            {
                var canAccessCustomer = await _accessService.CanAccessCustomer(
                    User,
                    customerNo,
                    cancellationToken);

                if (!canAccessCustomer)
                    return Forbid();
            }
            else
            {
                return BadRequest(
                    "Either Customer No or Site ID is required.");
            }
        }

        var result = await _sitesService.GetSites(
            query,
            cancellationToken);

        if (!result.Success)
        {
            return StatusCode(
                result.StatusCode,
                result.Error);
        }

        return Ok(result.Data);
    }
}