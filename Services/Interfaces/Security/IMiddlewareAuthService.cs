using mysystem_bff.Models.Admin;
using mysystem_bff.Models.Auth;
using mysystem_bff.Models.Portal;

namespace mysystem_bff.Services.Interfaces.Security;

public interface IMiddlewareAuthService
{
    Task<ServiceResult<string>> GetMiddlewareToken();

    // get full user details including associated customer(s)/site(s)
    Task<ServiceResult<PortalUserDetails>> GetUserDetailsAsync(
        AuthUserDto currentUser);
}