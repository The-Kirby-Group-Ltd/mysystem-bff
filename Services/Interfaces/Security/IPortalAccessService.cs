namespace mysystem_bff.Services.Interfaces.Security;

using System.Security.Claims;

public interface IPortalAccessService
{
    bool HasUnrestrictedAccess(ClaimsPrincipal user);

    Task<bool> CanAccessCustomer(
        ClaimsPrincipal user,
        string CustomerNo,
        CancellationToken cancellationToken = default);

    Task<bool> CanAccessSite(
        ClaimsPrincipal user,
        string siteId,
        CancellationToken cancellationToken = default);
}
