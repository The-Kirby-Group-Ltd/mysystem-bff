namespace mysystem_bff.Services.Interfaces.GenericData;

using mysystem_bff.Models.Admin;
using mysystem_bff.Models.Middleware.Calls;
using mysystem_bff.Models.Portal.Queries;
using mysystem_bff.Models.Portal.Responses;

public interface IMiddlewareCallActionsService
{
    Task<ServiceResult<PortalCallActionsResponse>> 
        GetCallActions(
            PortalCallActionsQuery query,
            CancellationToken ct = default);

    Task<ServiceResult<MiddlewareCalendarCallActionsResponse>>
        GetCalendarCallActions(
            string customerNo,
            DateTime appointmentFrom,
            DateTime appointmentTo,
            CancellationToken ct = default);
}
