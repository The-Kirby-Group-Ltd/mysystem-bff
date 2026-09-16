using mysystem_bff.Models.Admin;

namespace mysystem_bff.Services.Interfaces.Admin;

public interface IAdminUserPasswordService
{
    Task<ServiceResult<object>> ResetPassword(string userId, ResetPasswordRequest request);
}