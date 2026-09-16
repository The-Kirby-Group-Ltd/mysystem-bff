using mysystem_bff.Models.Admin;

namespace mysystem_bff.Services.Interfaces.Admin;

public interface IAdminUserCreateService
{
    Task<ServiceResult<UserListItemDto>> CreateUser(
        CreateUserRequest request,
        string actingUserId,
        IReadOnlyCollection<string> actingRoles);
}
