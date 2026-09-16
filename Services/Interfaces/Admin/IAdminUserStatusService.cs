using mysystem_bff.Models.Admin;

namespace mysystem_bff.Services.Interfaces.Admin;

public interface IAdminUserStatusService
{
    Task<ServiceResult<object>> UpdateUserStatus(string userId, UpdateUserStatusRequest request);
}
