using mysystem_bff.Models.Admin;
using mysystem_bff.Models.Portal.Queries;
using mysystem_bff.Models.Portal.Responses;

namespace mysystem_bff.Services.Interfaces.GenericData
{
    public interface IMiddlewareCallActionsService
    {
        Task<ServiceResult<PortalCallActionsResponse>> GetCallActions(
            PortalCallActionsQuery query,
            CancellationToken ct = default);
    }
}
