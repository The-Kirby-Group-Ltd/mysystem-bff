namespace mysystem_bff.Services.Services.Security;

using System.Security.Claims;
using Dapper;
using MySqlConnector;
using mysystem_bff.Services.Interfaces.Security;

public class PortalAccessService : IPortalAccessService
{
    private readonly MySqlConnection _db;

    public PortalAccessService(MySqlConnection db)
    {
        _db = db;
    }

    // unrestricted access for Kirby staff
    public bool HasUnrestrictedAccess(ClaimsPrincipal user)
    {
        return user.IsInRole("Administrator")
            || user.IsInRole("Staff")
            || user.IsInRole("Engineer");
    }

    // customer access 

    public async Task<bool> CanAccessCustomer(
        ClaimsPrincipal user,
        string customerNo,
        CancellationToken ct)
    {
        // kirby staff unrestricted
        if (HasUnrestrictedAccess(user))
            return true;

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var cleanCustomerNo = customerNo.Trim().ToUpperInvariant();

        // check good request
        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(cleanCustomerNo))
        {
            return false;
        }

        // request user's set customer nos
        var command = new CommandDefinition(
            """
            SELECT COUNT(1)
            FROM user_customer_access
            WHERE CAST(user_id AS CHAR) = @UserId
                AND UPPER(customer_no) = @CustomerNo;
            """,
            new
            {
                UserId = userId,
                CustomerNo = cleanCustomerNo
            },
            cancellationToken: ct
        );

        var count = await _db.ExecuteScalarAsync<int>(command);
        return count > 0;
    }

    // site access

    public async Task<bool> CanAccessSite(
        ClaimsPrincipal user,
        string siteId,
        CancellationToken ct)
    {
        if (HasUnrestrictedAccess(user))
            return true;

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var cleanSiteId = siteId.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(userId) || 
            string.IsNullOrWhiteSpace(cleanSiteId))
        {
            return false;
        }

        var command = new CommandDefinition(
            """
            SELECT COUNT(1)
            FROM user_site_access
            WHERE CAST(user_id AS CHAR) = @UserId
                AND UPPER(site_id) = @SiteId;
            """,
            new
            {
                UserId = userId,
                SiteId = cleanSiteId
            }, cancellationToken: ct);

        var count = await _db.ExecuteScalarAsync<int>(command);
        return count > 0;
    }
}
