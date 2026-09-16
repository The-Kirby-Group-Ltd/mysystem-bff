namespace mysystem_bff.Services.Services.GenericData;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using mysystem_bff.Models.Admin;
using mysystem_bff.Models.Middleware.Sites;
using mysystem_bff.Models.Portal;
using mysystem_bff.Models.Portal.Queries;
using mysystem_bff.Models.Portal.Responses;
using mysystem_bff.Services.Interfaces.GenericData;
using mysystem_bff.Services.Interfaces.Security;

public class MiddlewareSitesService : IMiddlewareSitesService
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly IMiddlewareAuthService _middlewareAuthService;

    public MiddlewareSitesService(
        IConfiguration configuration,
        HttpClient httpClient,
        IMiddlewareAuthService middlewareAuthService)
    {
        _configuration = configuration;
        _httpClient = httpClient;
        _middlewareAuthService = middlewareAuthService;
    }

    public async Task<ServiceResult<PortalSitesResponse>> GetSites(
        PortalSitesQuery query,
        CancellationToken cancellationToken = default)
    {
        var customerNo = query.CustomerNo?.Trim().ToUpperInvariant() ?? "";
        var siteId = query.SiteId?.Trim().ToUpperInvariant() ?? "";

        if (string.IsNullOrWhiteSpace(customerNo) &&
            string.IsNullOrWhiteSpace(siteId))
        {
            return ServiceResult<PortalSitesResponse>.Fail(
                "Either Customer No or Site ID is required.",
                400);
        }

        var tokenResult =
            await _middlewareAuthService.GetMiddlewareToken();

        if (!tokenResult.Success ||
            string.IsNullOrWhiteSpace(tokenResult.Data))
        {
            return ServiceResult<PortalSitesResponse>.Fail(
                tokenResult.Error ?? "Unable to authenticate with middleware API.",
                tokenResult.StatusCode);
        }

        var baseUrl = _configuration["MiddlewareApi:BaseUrl"];

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return ServiceResult<PortalSitesResponse>.Fail(
                "Middleware API base URL is missing.",
                500);
        }

        var page = query.Page > 0 ? query.Page : 1;
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var parameters = new Dictionary<string, string?>
        {
            ["customerNo"] = customerNo,
            ["siteId"] = siteId,
            ["page"] = page.ToString(),
            ["pageSize"] = pageSize.ToString(),
            ["postCode"] = query.PostCode?.Trim().ToUpperInvariant(),
            ["status"] = query.Status?.Trim().ToUpperInvariant()
        };

        var queryString = string.Join(
            "&",
            parameters
                .Where(parameter =>
                    !string.IsNullOrWhiteSpace(parameter.Value))
                .Select(parameter =>
                    $"{Uri.EscapeDataString(parameter.Key)}=" +
                    $"{Uri.EscapeDataString(parameter.Value!)}"));

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{baseUrl}/api/sites?{queryString}");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                tokenResult.Data);

        using var response = await _httpClient.SendAsync(
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(
                cancellationToken);

            return ServiceResult<PortalSitesResponse>.Fail(
                string.IsNullOrWhiteSpace(error)
                    ? "Middleware sites request failed."
                    : error,
                (int)response.StatusCode);
        }

        var middlewareResponse =
            await response.Content
                .ReadFromJsonAsync<MiddlewareSitesResponse>(
                    cancellationToken: cancellationToken);

        if (middlewareResponse is null)
        {
            return ServiceResult<PortalSitesResponse>.Fail(
                "Middleware API returned an invalid sites response.",
                502);
        }

        var result = new PortalSitesResponse
        {
            Items = middlewareResponse.Items
                .Select(MapSite)
                .ToList(),

            Page = middlewareResponse.Page,
            PageSize = middlewareResponse.PageSize,
            Total = middlewareResponse.Total,
            HasMore = middlewareResponse.HasMore
        };

        return ServiceResult<PortalSitesResponse>.Ok(result);
    }

    private static PortalSiteDto MapSite(
        MiddlewareSite site)
    {
        return new PortalSiteDto
        {
            SiteId = site.SiteID ?? "",
            CustomerNo = site.CustomerNo ?? "",
            SiteName = site.SiteName ?? site.Name ?? "",
            Status = site.Status ?? "",
            Name = site.Name ?? "",
            Add1 = site.Add1 ?? "",
            Add2 = site.Add2 ?? "",
            Add3 = site.Add3 ?? "",
            Add4 = site.Add4 ?? "",
            PostCode = site.PostCode ?? "",
            PropertyReferenceNo =
                site.PropertyReferenceNo ?? ""
        };
    }
}