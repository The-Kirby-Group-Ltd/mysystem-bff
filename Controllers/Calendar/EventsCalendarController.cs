namespace mysystem_bff.Controllers.Calendar;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using mysystem_bff.Models.Portal.EventsCalendar;
using mysystem_bff.Services.Interfaces.Calendar;
using mysystem_bff.Services.Interfaces.Security;

[ApiController]
[Route("api/portal/events-calendar")]
[Authorize]
public class EventsCalendarController : ControllerBase
{

    // =========================================================
    // Construct
    // =========================================================

    private readonly ICalendarCallsService _callsService;
    private readonly ICalendarMaintenanceService _maintenanceService;
    private readonly IPortalAccessService _accessService;

    public EventsCalendarController(
        ICalendarCallsService callsService,
        ICalendarMaintenanceService maintenanceService,
        IPortalAccessService accessService)
    {
        _callsService = callsService;
        _maintenanceService = maintenanceService;
        _accessService = accessService;
    }

    // =========================================================
    // Call appointments
    // =========================================================

    [HttpGet("calls")]
    public async Task<ActionResult<List<PortalCalendarCallEventDto>>>
        GetCallEvents(
            [FromQuery] PortalCalendarCallsQuery query,
            CancellationToken ct)
    {
        var customerNo =
            query.CustomerNo?
                .Trim()
                .ToUpperInvariant()
            ?? "";

        if (string.IsNullOrWhiteSpace(customerNo))
        {
            return BadRequest(
                "Customer No is required.");
        }

        var canAccess =
            await _accessService.CanAccessCustomer(
                User,
                customerNo);

        if (!canAccess)
        {
            return Forbid();
        }

        query.CustomerNo =
            customerNo;

        var result =
            await _callsService.GetEventsAsync(
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

    // =========================================================
    // Maintenance data
    // =========================================================

    [HttpGet("maintenance")]
    public async Task<ActionResult<List<PortalCalendarMaintenanceEventDto>>>
        GetMaintenanceEvents(
            [FromQuery] PortalCalendarQuery query,
            CancellationToken ct)
    {
        var accessResult = await CheckAccess(
            query.CustomerNo,
            query.SiteId,
            ct);

        if (accessResult is not null)
            return accessResult;

        var result = await _maintenanceService.GetEventsAsync(
            query,
            ct);

        if (!result.Success)
            return StatusCode(
                result.StatusCode,
                result.Error);

        return Ok(result.Data);
    }

    // =========================================================
    // Access helper
    // =========================================================

    private async Task<ActionResult?> CheckAccess(
        string? customerNo,
        string? siteId,
        CancellationToken ct)
    {
        var accessResult = false;
        var accessType = "dataset";

        if (!string.IsNullOrWhiteSpace(customerNo))
        {
            accessType = "customer";

            var cleanCustomerNo =
                customerNo?
                    .Trim()
                    .ToUpperInvariant() ?? "";

            accessResult =
                    await _accessService.CanAccessCustomer(
                        User,
                        cleanCustomerNo);
        } 
        else
        {
            accessType = "site";

            var cleanSiteId =
                siteId?
                    .Trim()
                    .ToUpperInvariant() ?? "";

            accessResult =
                await _accessService.CanAccessSite(
                    User,
                    cleanSiteId);
        }

        if (!accessResult)
            return Forbid($"Access to this {accessType} is not permitted.");

        return null;
    }
}