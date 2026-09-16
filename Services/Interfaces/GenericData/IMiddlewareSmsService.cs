using mysystem_bff.Models.Admin;
using mysystem_bff.Models.Portal;
using mysystem_bff.Models.Portal.Queries;
using mysystem_bff.Models.Portal.Responses;

namespace mysystem_bff.Services.Interfaces.GenericData
{
    public interface IMiddlewareSmsService
    {
        // =====================================================
        // Existing single-system maintenance lookup
        // =====================================================

        Task<ServiceResult<PortalSMSResponse>> GetSms(
            PortalSMSQuery query,
            CancellationToken ct);

        // =====================================================
        // Maintenance dashboard aggregation
        // =====================================================

        Task<ServiceResult<List<PortalSMSDto>>>
            GetMaintenanceSchedulesForSites(
                string customerNo,
                IReadOnlyCollection<string> siteIds,
                DateTime? nextMaintenanceFrom,
                DateTime? nextMaintenanceTo,
                CancellationToken ct = default);
    }
}