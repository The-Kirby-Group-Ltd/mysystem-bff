using mysystem_bff.Models.Admin;
using mysystem_bff.Models.Portal.DashboardData;

namespace mysystem_bff.Services.Interfaces.Dashboard;

public interface IMaintenanceDashboardService
{
    Task<ServiceResult<PortalMaintenanceDashboardDataDto>>
        GetDashboardDataAsync(
            PortalDashboardDataQuery query,
            CancellationToken ct = default);

    Task<ServiceResult<PortalDashboardMaintenanceItemsResponse>>
        GetDashboardItemsAsync(
            PortalDashboardMaintenanceItemsQuery query,
            CancellationToken ct = default);
}