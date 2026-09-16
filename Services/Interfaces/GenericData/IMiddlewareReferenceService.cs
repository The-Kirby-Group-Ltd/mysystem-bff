using mysystem_bff.Models.Admin;
using mysystem_bff.Models.Portal.Reference;
using mysystem_bff.Models.Portal.Responses;

namespace mysystem_bff.Services.Interfaces.GenericData;

public interface IMiddlewareReferenceService
{
    Task<ServiceResult<PortalPagedResponse<PortalSystemTypeDto>>> GetSystemTypes(
        PortalReferenceQuery query,
        CancellationToken ct = default);

    Task<ServiceResult<PortalPagedResponse<PortalEngineerDto>>> GetEngineers(
        PortalReferenceQuery query,
        CancellationToken ct = default);
 
    Task<ServiceResult<PortalFailedToRespondReasonDto>> GetFailedToRespondReason(
        string code,
        CancellationToken ct = default);
}