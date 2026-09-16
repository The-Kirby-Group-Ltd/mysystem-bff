using mysystem_bff.Models.Admin;

namespace mysystem_bff.Services.Interfaces.Admin;

public interface IAdminUserReadService
{
    Task<List<UserListItemDto>> GetUsers(UserFilters filters);
    Task<UserListItemDto?> GetUserById(string userId);
}
