namespace mysystem_bff.Controllers.GenericData;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using mysystem_bff.Models.Portal.Queries;
using mysystem_bff.Services.Interfaces.GenericData;
using mysystem_bff.Services.Interfaces.Security;

[ApiController]
[Route("api/portal/site-systems")]
[Authorize]
public class PortalSiteSystemsController : ControllerBase
{
    private readonly IMiddlewareSiteSystemsService _siteSystemsService;
    private readonly IPortalAccessService _accessService;

    public PortalSiteSystemsController(
        IMiddlewareSiteSystemsService siteSystemsService, 
        IPortalAccessService accessService)
    {
        _siteSystemsService = siteSystemsService;
        _accessService = accessService;
    }

    [HttpGet]
    public async Task<IActionResult> GetSiteSystems(
        [FromQuery] PortalSiteSystemsQuery query,
        CancellationToken ct)
    {
        var siteId = query.SiteId?.Trim().ToUpperInvariant() ?? "";

        if (string.IsNullOrWhiteSpace(siteId))
            return BadRequest("Site ID must be given to request Site Systems");

        if (!_accessService.HasUnrestrictedAccess(User))
        {
            var canAccessSite = await _accessService.CanAccessSite(User, siteId, ct);

            if (!canAccessSite)
                return Unauthorized("You do not have permission to access this site's systems.");
        }

        query.SiteId = siteId;

        var result = await _siteSystemsService.GetSiteSystemsAsync(query, ct);

        if (!result.Success)
        {
            return StatusCode(
                result.StatusCode,
                result.Error);
        }

        return Ok(result.Data);
    }
}
