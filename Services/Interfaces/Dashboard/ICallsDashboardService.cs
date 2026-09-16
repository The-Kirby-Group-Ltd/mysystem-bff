using mysystem_bff.Models.Admin;
using mysystem_bff.Models.Portal.DashboardData;

namespace mysystem_bff.Services.Interfaces.Dashboard;

public interface ICallsDashboardService
{
    Task<ServiceResult<PortalCallsDashboardDataDto>>
        GetDashboardDataAsync(
            PortalDashboardDataQuery query,
            CancellationToken ct = default);

    Task<ServiceResult<PortalDashboardCallsItemsResponse>>
        GetDashboardItemsAsync(
            PortalDashboardCallsItemsQuery query,
            CancellationToken ct = default);
}