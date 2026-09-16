using mysystem_bff.Models.Admin;
using mysystem_bff.Models.Portal;
using mysystem_bff.Models.Middleware;
using mysystem_bff.Models.Auth;
using Dapper;
using MySqlConnector;
using mysystem_bff.Services.Interfaces.Security;

namespace mysystem_bff.Services.Services.Security;

public class MiddlewareAuthService : IMiddlewareAuthService
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly MySqlConnection _db;

    public MiddlewareAuthService(
        IConfiguration configuration,
        HttpClient httpClient,
        MySqlConnection db)
    {
        _configuration = configuration;
        _httpClient = httpClient;
        _db = db;
    }

    public async Task<ServiceResult<string>> GetMiddlewareToken()
    {
        var baseUrl = _configuration["MiddlewareApi:BaseUrl"];
        var username = _configuration["MiddlewareApi:Username"];
        var password = _configuration["MiddlewareApi:Password"];

        if (
            string.IsNullOrWhiteSpace(baseUrl) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password)
        )
        {
            return ServiceResult<string>.Fail(
                "Middleware API configuration is missing.",
                500
            );
        }

        var loginResponse = await _httpClient.PostAsJsonAsync(
            $"{baseUrl}/api/auth/login",
            new
            {
                username,
                password
            }
        );

        if (!loginResponse.IsSuccessStatusCode)
        {
            return ServiceResult<string>.Fail(
                "Failed to authenticate with middleware API.",
                502
            );
        }

        var tokenResponse =
            await loginResponse.Content.ReadFromJsonAsync<MiddlewareTokenResponse>();

        if (tokenResponse is null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
        {
            return ServiceResult<string>.Fail(
                "Middleware API returned an invalid token response.",
                502
            );
        }

        return ServiceResult<string>.Ok(tokenResponse.AccessToken);
    }



    public async Task<ServiceResult<PortalUserDetails>> GetUserDetailsAsync(
        AuthUserDto currentUser)
    {
        if (string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            return ServiceResult<PortalUserDetails>.Fail(
                "Unable to resolve the current user.",
                401);
        }

        var user = new PortalUserDetails
        {
            UserId = currentUser.UserId,
            Username = currentUser.Username,
            Email = currentUser.Email,
            FirstName = currentUser.FirstName,
            LastName = currentUser.LastName,
            Roles = currentUser.Roles
        };

        try
        {
            // =====================================================
            // Customer access
            // =====================================================

            var customerNos =
                await _db.QueryAsync<string>(
                    """
                SELECT DISTINCT UPPER(customer_no)
                FROM user_customer_access
                WHERE CAST(user_id AS CHAR) = @UserId
                ORDER BY customer_no;
                """,
                    new
                    {
                        UserId = currentUser.UserId
                    });

            user.CustomerNos =
                customerNos
                    .Where(customerNo =>
                        !string.IsNullOrWhiteSpace(customerNo))
                    .Select(customerNo =>
                        customerNo
                            .Trim()
                            .ToUpperInvariant())
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            // =====================================================
            // Site access
            // =====================================================

            var siteIds =
                await _db.QueryAsync<string>(
                    """
                SELECT DISTINCT UPPER(site_id)
                FROM user_site_access
                WHERE CAST(user_id AS CHAR) = @UserId
                ORDER BY site_id;
                """,
                    new
                    {
                        UserId = currentUser.UserId
                    });

            user.SiteIds =
                siteIds
                    .Where(siteId =>
                        !string.IsNullOrWhiteSpace(siteId))
                    .Select(siteId =>
                        siteId
                            .Trim()
                            .ToUpperInvariant())
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            return ServiceResult<PortalUserDetails>.Ok(
                user);
        }
        catch (Exception ex)
        {
            return ServiceResult<PortalUserDetails>.Fail(
                $"Unable to retrieve portal user access details. {ex.Message}",
                500);
        }
    }
}