using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using mysystem_bff.Models.Portal.Queries;
using mysystem_bff.Models.Portal.Responses;
using mysystem_bff.Services.Interfaces.GenericData;
using mysystem_bff.Services.Interfaces.Security;

namespace mysystem_bff.Controllers.GenericData
{
    [ApiController]
    [Route("api/portal/system-maint-schedules")]
    [Authorize]
    public class PortalSmsController : ControllerBase
    {
        private readonly IMiddlewareSmsService _smsService;
        private readonly IPortalAccessService _accessService;

        public PortalSmsController(IMiddlewareSmsService smsService, IPortalAccessService accessService)
        {
            _smsService = smsService;
            _accessService = accessService;
        }

        [HttpGet]
        public async Task<ActionResult<PortalSMSResponse>> GetSms(
            [FromQuery] PortalSMSQuery query,
            CancellationToken ct = default)
        {
            var siteId = query.SiteId?.Trim().ToUpperInvariant() ?? "";

            if (string.IsNullOrWhiteSpace(siteId) ||
                query.SystemNo < 1)
            {
                return BadRequest("Request does not contain both Site ID and System No (Required).");
            }

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
            }

            var result = await _smsService.GetSms(
                query, ct);

            if (!result.Success)
            {
                return StatusCode(
                    result.StatusCode,
                    result.Error);
            }

            return Ok(result.Data);
        }
    }
}
