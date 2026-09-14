using mysystem_bff.Models.Admin;
using mysystem_bff.Models.Portal.DashboardData;

namespace mysystem_bff.Services.Interfaces;

public interface ISlaDashboardService
{
    Task<ServiceResult<PortalSlaDashboardDataDto>>
        GetDashboardDataAsync(
            PortalDashboardDataQuery query,
            CancellationToken ct = default);

    Task<ServiceResult<PortalDashboardSlaItemsResponse>>
        GetDashboardItemsAsync(
            PortalDashboardSlaItemsQuery query,
            CancellationToken ct = default);
}