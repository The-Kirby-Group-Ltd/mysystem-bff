using mysystem_bff.Models.Admin;
using mysystem_bff.Models.Portal.DashboardData;

namespace mysystem_bff.Services.Interfaces.Dashboard;

public interface IDashboardDataService
{
    // =========================================================
    // Calls dashboard
    // =========================================================

    Task<ServiceResult<PortalCallsDashboardDataDto>>
        GetCallsDashboardDataAsync(
            PortalDashboardDataQuery query,
            CancellationToken ct = default);

    Task<ServiceResult<PortalDashboardCallsItemsResponse>>
        GetCallsDashboardItemsAsync(
            PortalDashboardCallsItemsQuery query,
            CancellationToken ct = default);

    // =========================================================
    // Maintenance dashboard
    // =========================================================

    Task<ServiceResult<PortalMaintenanceDashboardDataDto>>
        GetMaintenanceDashboardDataAsync(
            PortalDashboardDataQuery query,
            CancellationToken ct = default);

    Task<ServiceResult<PortalDashboardMaintenanceItemsResponse>>
        GetMaintenanceDashboardItemsAsync(
            PortalDashboardMaintenanceItemsQuery query,
            CancellationToken ct = default);

    // =========================================================
    // SLA dashboard
    // =========================================================

    Task<ServiceResult<PortalSlaDashboardDataDto>>
    GetSlaDashboardDataAsync(
        PortalDashboardDataQuery query,
        CancellationToken ct = default);

    Task<ServiceResult<PortalDashboardSlaItemsResponse>>
        GetSlaDashboardItemsAsync(
            PortalDashboardSlaItemsQuery query,
            CancellationToken ct = default);
}