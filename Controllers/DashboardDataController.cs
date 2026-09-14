using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using mysystem_bff.Models.Portal.DashboardData;
using mysystem_bff.Services.Interfaces;
using mysystem_bff.Services.Services;

namespace mysystem_bff.Controllers;

[ApiController]
[Authorize]
[Route("api/portal/dashboard")]
public class DashboardDataController : ControllerBase
{
    private readonly IDashboardDataService _dashboardService;
    private readonly IPortalAccessService _accessService;

    public DashboardDataController(
        IDashboardDataService dashboardService,
        IPortalAccessService accessService)
    {
        _dashboardService = dashboardService;
        _accessService = accessService;
    }

    // =========================================================
    // Calls dashboard summary
    // =========================================================

    [HttpGet("calls-dashboard")]
    public async Task<ActionResult<PortalCallsDashboardDataDto>>
        GetCallsDashboardData(
            [FromQuery] PortalDashboardDataQuery query,
            CancellationToken ct)
    {
        var accessResult =
            await CheckDashboardAccess(
                query.CustomerNo,
                query.SiteId,
                ct);

        if (accessResult is not null)
        {
            return accessResult;
        }

        query.CustomerNo =
            query.CustomerNo?
                .Trim()
                .ToUpperInvariant()
            ?? "";

        query.SiteId =
            query.SiteId?
                .Trim()
                .ToUpperInvariant()
            ?? "";

        var result =
            await _dashboardService
                .GetCallsDashboardDataAsync(
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
    // Calls dashboard supporting records
    // =========================================================

    [HttpGet("calls-dashboard/items")]
    public async Task<ActionResult<PortalDashboardCallsItemsResponse>>
        GetCallsDashboardItems(
            [FromQuery] PortalDashboardCallsItemsQuery query,
            CancellationToken ct)
    {
        var accessResult =
            await CheckDashboardAccess(
                query.CustomerNo,
                query.SiteId,
                ct);

        if (accessResult is not null)
        {
            return accessResult;
        }

        query.CustomerNo =
            query.CustomerNo?
                .Trim()
                .ToUpperInvariant()
            ?? "";

        query.SiteId =
            query.SiteId?
                .Trim()
                .ToUpperInvariant()
            ?? "";

        var result =
            await _dashboardService
                .GetCallsDashboardItemsAsync(
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
    // Maintenance dashboard summary
    // =========================================================

    [HttpGet("maintenance-dashboard")]
    public async Task<ActionResult<PortalMaintenanceDashboardDataDto>>
        GetMaintenanceDashboardData(
            [FromQuery] PortalDashboardDataQuery query,
            CancellationToken ct)
    {
        // access rights
        var accessResult =
            await CheckDashboardAccess(
                query.CustomerNo,
                query.SiteId,
                ct);

        if (accessResult is not null)
        {
            return accessResult;
        }

        // get data
        query.CustomerNo =
            query.CustomerNo?
                .Trim()
                .ToUpperInvariant()
            ?? "";

        query.SiteId =
            query.SiteId?
                .Trim()
                .ToUpperInvariant()
            ?? "";

        var result =
            await _dashboardService
                .GetMaintenanceDashboardDataAsync(
                    query,
                    ct);

        // response to frontend
        if (!result.Success)
        {
            return StatusCode(
                result.StatusCode,
                result.Error);
        }

        return Ok(result.Data);
    }

    // =========================================================
    // Maintenance dashboard supporting records
    // =========================================================

    [HttpGet("maintenance-dashboard/items")]
    public async Task<ActionResult<PortalDashboardMaintenanceItemsResponse>>
    GetMaintenanceDashboardItems(
        [FromQuery] PortalDashboardMaintenanceItemsQuery query,
        CancellationToken ct)
    {
        var accessResult =
            await CheckDashboardAccess(
                query.CustomerNo,
                query.SiteId,
                ct);

        if (accessResult is not null)
        {
            return accessResult;
        }

        query.CustomerNo =
            query.CustomerNo?
                .Trim()
                .ToUpperInvariant()
            ?? "";

        query.SiteId =
            query.SiteId?
                .Trim()
                .ToUpperInvariant()
            ?? "";

        var result =
            await _dashboardService
                .GetMaintenanceDashboardItemsAsync(
                    query,
                    ct);

        if (!result.Success)
        {
            return StatusCode(
                result.StatusCode,
                result.Error);
        }

        return Ok(
            result.Data);
    }

    // =========================================================
    // SLA dashboard summary
    // =========================================================

    [HttpGet("sla-dashboard")]
    public async Task<ActionResult<PortalSlaDashboardDataDto>>
        GetSlaDashboardData(
            [FromQuery] PortalDashboardDataQuery query,
            CancellationToken ct)
    {
        var accessResult =
            await CheckDashboardAccess(
                query.CustomerNo,
                query.SiteId,
                ct);

        if (accessResult is not null)
        {
            return accessResult;
        }

        query.CustomerNo =
            query.CustomerNo?
                .Trim()
                .ToUpperInvariant()
            ?? "";

        query.SiteId =
            query.SiteId?
                .Trim()
                .ToUpperInvariant()
            ?? "";

        var result =
            await _dashboardService
                .GetSlaDashboardDataAsync(
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
    // SLA dashboard supporting calls
    // =========================================================

    [HttpGet("sla-dashboard/items")]
    public async Task<ActionResult<PortalDashboardSlaItemsResponse>>
        GetSlaDashboardItems(
            [FromQuery] PortalDashboardSlaItemsQuery query,
            CancellationToken ct)
    {
        var accessResult =
            await CheckDashboardAccess(
                query.CustomerNo,
                query.SiteId,
                ct);

        if (accessResult is not null)
        {
            return accessResult;
        }

        query.CustomerNo =
            query.CustomerNo?
                .Trim()
                .ToUpperInvariant()
            ?? "";

        query.SiteId =
            query.SiteId?
                .Trim()
                .ToUpperInvariant()
            ?? "";

        var result =
            await _dashboardService
                .GetSlaDashboardItemsAsync(
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
    // Shared access check
    // =========================================================

    private async Task<ActionResult?> CheckDashboardAccess(
        string? customerNo,
        string? siteId,
        CancellationToken ct)
    {
        var cleanCustomerNo =
            customerNo?
                .Trim()
                .ToUpperInvariant()
            ?? "";

        var cleanSiteId =
            siteId?
                .Trim()
                .ToUpperInvariant()
            ?? "";

        if (string.IsNullOrWhiteSpace(cleanCustomerNo) &&
            string.IsNullOrWhiteSpace(cleanSiteId))
        {
            return BadRequest(
                "Either Customer No or Site ID is required.");
        }

        if (_accessService.HasUnrestrictedAccess(User))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(cleanSiteId))
        {
            var canAccessSite =
                await _accessService.CanAccessSite(
                    User,
                    cleanSiteId,
                    ct);

            if (!canAccessSite)
            {
                return Forbid();
            }

            return null;
        }

        var canAccessCustomer =
            await _accessService.CanAccessCustomer(
                User,
                cleanCustomerNo,
                ct);

        if (!canAccessCustomer)
        {
            return Forbid();
        }

        return null;
    }
}