namespace mysystem_bff.Services.Interfaces.GenericData;

using mysystem_bff.Models.Admin;
using mysystem_bff.Models.Portal.Queries;
using mysystem_bff.Models.Portal.Responses;

public interface IMiddlewareCallsService
{
    Task<ServiceResult<PortalCallsResponse>> GetCalls(
        PortalCallsQuery query,
        CancellationToken cancellationToken = default);
}
