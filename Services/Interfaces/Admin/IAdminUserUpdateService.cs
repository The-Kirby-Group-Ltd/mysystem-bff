using mysystem_bff.Models.Admin;

namespace mysystem_bff.Services.Interfaces.Admin;

public interface IAdminUserUpdateService
{
    Task<ServiceResult<UserListItemDto>> UpdateUser(
        string userId, 
        UpdateUserRequest request,
        string actingUserId,
        IReadOnlyCollection<string> actingRoles);
}