using Dapper;
using MySqlConnector;
using mysystem_bff.Models.Admin;
using mysystem_bff.Services.Interfaces.Admin;

namespace mysystem_bff.Services.Services.Admin;

public class AdminUserStatusService : IAdminUserStatusService
{
    private readonly MySqlConnection _db;

    public AdminUserStatusService(MySqlConnection db)
    {
        _db = db;
    }

    public async Task<ServiceResult<object>> UpdateUserStatus(
        string userId,
        UpdateUserStatusRequest request)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return ServiceResult<object>.Fail("User ID is required.", 400);

        var affectedRows = await _db.ExecuteAsync(
            """
            UPDATE users
            SET 
                is_active = @IsActive,
                updated_at = CURRENT_TIMESTAMP
            WHERE CAST(user_id AS CHAR) = @UserId;
            """,
            new
            {
                UserId = userId,
                request.IsActive
            }
        );

        if (affectedRows == 0)
            return ServiceResult<object>.Fail("User not found.", 404);

        return ServiceResult<object>.Ok(new
        {
            userId,
            isActive = request.IsActive
        });
    }
}