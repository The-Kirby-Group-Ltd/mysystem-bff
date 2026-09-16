using mysystem_bff.Models.Admin;
using mysystem_bff.Models.Auth;

namespace mysystem_bff.Services.Interfaces.Security
{
    public interface IPasswordUpdateService
    {
        Task<ServiceResult<bool>> SendVerificationCodeAsync(
            string userId,
            CancellationToken ct = default);

        Task<ServiceResult<bool>> ChangePasswordAsync(
            string userId,
            ChangePasswordRequest request,
            CancellationToken ct = default);
    }
}