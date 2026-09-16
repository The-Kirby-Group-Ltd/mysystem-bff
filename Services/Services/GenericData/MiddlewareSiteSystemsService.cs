using System.Net.Http.Headers;
using System.Net.Http.Json;
using mysystem_bff.Models.Admin;
using mysystem_bff.Models.Middleware.Systems;
using mysystem_bff.Models.Portal;
using mysystem_bff.Models.Portal.Queries;
using mysystem_bff.Models.Portal.Responses;
using mysystem_bff.Services.Interfaces.GenericData;
using mysystem_bff.Services.Interfaces.Security;

namespace mysystem_bff.Services.Services.GenericData
{
    public class MiddlewareSiteSystemsService
        : IMiddlewareSiteSystemsService
    {
        /*
         * MMAPI is shared with other services.
         *
         * Customer-wide dashboards can involve hundreds of sites, so keep
         * the number of simultaneous site-system requests deliberately low.
         */
        private const int DashboardSiteConcurrency = 4;

        /*
         * The BFF's ordinary site-system endpoint currently caps pages at 100.
         *
         * MMAPI applies filtering before pagination, so for the dashboard we
         * request maintained systems only and retrieve further pages if a site
         * genuinely contains more than 100 maintained systems.
         */
        private const int DashboardPageSize = 100;

        private const int MaximumPagesPerSite = 100;

        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;
        private readonly IMiddlewareAuthService _authService;

        public MiddlewareSiteSystemsService(
            IConfiguration config,
            HttpClient httpClient,
            IMiddlewareAuthService authService)
        {
            _config = config;
            _httpClient = httpClient;
            _authService = authService;
        }

        // =========================================================
        // SINGLE-SITE FLOW
        // =========================================================

        public async Task<ServiceResult<PortalSiteSystemsResponse>>
            GetSiteSystemsAsync(
                PortalSiteSystemsQuery query,
                CancellationToken ct = default)
        {
            var siteId =
                query.SiteId?
                    .Trim()
                    .ToUpperInvariant()
                ?? "";

            if (string.IsNullOrWhiteSpace(siteId))
            {
                return ServiceResult<PortalSiteSystemsResponse>.Fail(
                    "Site ID is required.",
                    400);
            }

            var contextResult =
                await GetMiddlewareRequestContext(ct);

            if (!contextResult.Success ||
                contextResult.Data is null)
            {
                return ServiceResult<PortalSiteSystemsResponse>.Fail(
                    contextResult.Error ??
                        "Unable to authenticate with middleware API.",
                    contextResult.StatusCode);
            }

            var page =
                query.Page > 0
                    ? query.Page
                    : 1;

            var pageSize =
                Math.Clamp(
                    query.PageSize,
                    1,
                    100);

            return await SendSiteSystemsRequest(
                contextResult.Data.BaseUrl,
                contextResult.Data.Token,
                new PortalSiteSystemsQuery
                {
                    SiteId = siteId,

                    SystemNo =
                        query.SystemNo,

                    SystemCode =
                        query.SystemCode?
                            .Trim()
                            .ToUpperInvariant(),

                    Status =
                        query.Status?
                            .Trim()
                            .ToUpperInvariant(),

                    Maintained_YN =
                        query.Maintained_YN?
                            .Trim()
                            .ToUpperInvariant(),

                    Page = page,
                    PageSize = pageSize
                },
                ct);
        }

        // =========================================================
        // DASHBOARD MULTI-SITE FLOW
        // =========================================================

        public async Task<ServiceResult<List<PortalSiteSystemDto>>>
            GetSiteSystemsForSitesAsync(
                IReadOnlyCollection<string> siteIds,
                string maintainedYN,
                CancellationToken ct = default)
        {
            var cleanSiteIds =
                siteIds
                    .Select(siteId =>
                        siteId?
                            .Trim()
                            .ToUpperInvariant()
                        ?? "")
                    .Where(siteId =>
                        !string.IsNullOrWhiteSpace(siteId))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            if (cleanSiteIds.Count == 0)
            {
                return ServiceResult<List<PortalSiteSystemDto>>.Ok(
                    []);
            }

            var cleanMaintainedYN =
                maintainedYN?
                    .Trim()
                    .ToUpperInvariant()
                ?? "";

            var contextResult =
                await GetMiddlewareRequestContext(ct);

            if (!contextResult.Success ||
                contextResult.Data is null)
            {
                return ServiceResult<List<PortalSiteSystemDto>>.Fail(
                    contextResult.Error ??
                        "Unable to authenticate with middleware API.",
                    contextResult.StatusCode);
            }

            using var concurrencyGate =
                new SemaphoreSlim(
                    DashboardSiteConcurrency,
                    DashboardSiteConcurrency);

            var tasks =
                cleanSiteIds
                    .Select(async siteId =>
                    {
                        await concurrencyGate.WaitAsync(ct);

                        try
                        {
                            try
                            {
                                var result =
                                    await GetAllSiteSystemsForSite(
                                        contextResult.Data.BaseUrl,
                                        contextResult.Data.Token,
                                        siteId,
                                        cleanMaintainedYN,
                                        ct);

                                return new SiteSystemsBatchResult(
                                    siteId,
                                    result);
                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                return new SiteSystemsBatchResult(
                                    siteId,
                                    ServiceResult<List<PortalSiteSystemDto>>
                                        .Fail(
                                            ex.Message,
                                            502));
                            }
                        }
                        finally
                        {
                            concurrencyGate.Release();
                        }
                    })
                    .ToList();

            var results =
                await Task.WhenAll(tasks);

            /*
             * Do not silently produce a partial dashboard.
             *
             * If one site fails, the whole board fails, 
             * this stops partial/incorrect data
             */
            var failedSite =
                results.FirstOrDefault(result =>
                    !result.Result.Success);

            if (failedSite is not null)
            {
                return ServiceResult<List<PortalSiteSystemDto>>.Fail(
                    $"Unable to retrieve site systems for site " +
                    $"'{failedSite.SiteId}'. " +
                    (failedSite.Result.Error ??
                        "Middleware site systems request failed."),
                    failedSite.Result.StatusCode);
            }

            var systems =
                results
                    .SelectMany(result =>
                        result.Result.Data ?? [])
                    .GroupBy(
                        system =>
                            $"{CleanCode(system.SiteId)}|" +
                            $"{system.SystemNo}",
                        StringComparer.OrdinalIgnoreCase)
                    .Select(group =>
                        group.First())
                    .ToList();

            return ServiceResult<List<PortalSiteSystemDto>>.Ok(
                systems);
        }

        // =========================================================
        // RETRIEVE ALL SYSTEMS FOR ONE SITE
        // =========================================================

        private async Task<ServiceResult<List<PortalSiteSystemDto>>>
            GetAllSiteSystemsForSite(
                string baseUrl,
                string token,
                string siteId,
                string maintainedYN,
                CancellationToken ct)
        {
            var systems =
                new List<PortalSiteSystemDto>();

            var page = 1;
            var hasMore = true;

            while (
                hasMore &&
                page <= MaximumPagesPerSite)
            {
                var result =
                    await SendSiteSystemsRequest(
                        baseUrl,
                        token,
                        new PortalSiteSystemsQuery
                        {
                            SiteId = siteId,

                            Maintained_YN =
                                string.IsNullOrWhiteSpace(maintainedYN)
                                    ? null
                                    : maintainedYN,

                            Page = page,
                            PageSize = DashboardPageSize
                        },
                        ct);

                if (!result.Success)
                {
                    return ServiceResult<List<PortalSiteSystemDto>>.Fail(
                        result.Error ??
                            "Unable to retrieve site systems from middleware.",
                        result.StatusCode);
                }

                if (result.Data is null)
                {
                    return ServiceResult<List<PortalSiteSystemDto>>.Fail(
                        "Middleware returned no site-system data.",
                        502);
                }

                systems.AddRange(
                    result.Data.Items);

                hasMore =
                    result.Data.HasMore;

                page++;
            }

            if (hasMore)
            {
                return ServiceResult<List<PortalSiteSystemDto>>.Fail(
                    $"Site-system retrieval for site '{siteId}' exceeded " +
                    $"the maximum page limit.",
                    502);
            }

            return ServiceResult<List<PortalSiteSystemDto>>.Ok(
                systems);
        }

        // =========================================================
        // SHARED MMAPI REQUEST
        // =========================================================

        private async Task<ServiceResult<PortalSiteSystemsResponse>>
            SendSiteSystemsRequest(
                string baseUrl,
                string token,
                PortalSiteSystemsQuery query,
                CancellationToken ct)
        {
            var parameters =
                new Dictionary<string, string?>
                {
                    ["siteId"] =
                        query.SiteId,

                    ["systemNo"] =
                        query.SystemNo?.ToString(),

                    ["systemCode"] =
                        query.SystemCode,

                    ["status"] =
                        query.Status,

                    ["maintained_YN"] =
                        query.Maintained_YN,

                    ["page"] =
                        query.Page.ToString(),

                    ["pageSize"] =
                        query.PageSize.ToString()
                };

            var queryString =
                string.Join(
                    "&",
                    parameters
                        .Where(parameter =>
                            !string.IsNullOrWhiteSpace(
                                parameter.Value))
                        .Select(parameter =>
                            $"{Uri.EscapeDataString(parameter.Key)}=" +
                            $"{Uri.EscapeDataString(parameter.Value!)}"));

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    $"{baseUrl}/api/site-systems?{queryString}");

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    token);

            using var response =
                await _httpClient.SendAsync(
                    request,
                    ct);

            if (!response.IsSuccessStatusCode)
            {
                var error =
                    await response.Content.ReadAsStringAsync(ct);

                return ServiceResult<PortalSiteSystemsResponse>.Fail(
                    string.IsNullOrWhiteSpace(error)
                        ? "Middleware site systems request failed."
                        : error,
                    (int)response.StatusCode);
            }

            var middlewareResponse =
                await response.Content
                    .ReadFromJsonAsync<MiddlewareSiteSystemsResponse>(
                        cancellationToken: ct);

            if (middlewareResponse is null)
            {
                return ServiceResult<PortalSiteSystemsResponse>.Fail(
                    "Middleware API returned an invalid site systems response.",
                    502);
            }

            var result =
                new PortalSiteSystemsResponse
                {
                    Items =
                        middlewareResponse.Items
                            .Select(MapSiteSystem)
                            .ToList(),

                    Page =
                        middlewareResponse.Page,

                    PageSize =
                        middlewareResponse.PageSize,

                    Total =
                        middlewareResponse.Total,

                    HasMore =
                        middlewareResponse.HasMore
                };

            return ServiceResult<PortalSiteSystemsResponse>.Ok(
                result);
        }

        // =========================================================
        // AUTH / CONFIG
        // =========================================================

        private async Task<ServiceResult<MiddlewareRequestContext>>
            GetMiddlewareRequestContext(
                CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var tokenResult =
                await _authService.GetMiddlewareToken();

            if (!tokenResult.Success ||
                string.IsNullOrWhiteSpace(tokenResult.Data))
            {
                return ServiceResult<MiddlewareRequestContext>.Fail(
                    tokenResult.Error ??
                        "Unable to authenticate with middleware API.",
                    tokenResult.StatusCode > 0
                        ? tokenResult.StatusCode
                        : 500);
            }

            var baseUrl =
                _config["MiddlewareApi:BaseUrl"];

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return ServiceResult<MiddlewareRequestContext>.Fail(
                    "Middleware API base URL is missing.",
                    500);
            }

            return ServiceResult<MiddlewareRequestContext>.Ok(
                new MiddlewareRequestContext(
                    baseUrl,
                    tokenResult.Data));
        }

        // =========================================================
        // MAPPING
        // =========================================================

        private static PortalSiteSystemDto MapSiteSystem(
            MiddlewareSiteSystem ss)
        {
            return new PortalSiteSystemDto
            {
                SystemNo =
                    ss.SystemNo,

                SiteId =
                    CleanCode(
                        ss.SiteId),

                SystemCode =
                    CleanCode(
                        ss.SystemCode),

                Status =
                    CleanCode(
                        ss.Status),

                Maintained_YN =
                    CleanCode(
                        ss.Maintained_YN),

                CommissionedDate =
                    ss.CommissionedDate,

                LastMaintenanceDate =
                    ss.LastMaintenanceDate,

                NextMaintenanceDate =
                    ss.NextMaintenanceDate
            };
        }

        private static string CleanCode(
            string? value)
        {
            return value?
                .Trim()
                .ToUpperInvariant()
                ?? "";
        }

        private sealed record MiddlewareRequestContext(
            string BaseUrl,
            string Token);

        private sealed record SiteSystemsBatchResult(
            string SiteId,
            ServiceResult<List<PortalSiteSystemDto>> Result);
    }
}