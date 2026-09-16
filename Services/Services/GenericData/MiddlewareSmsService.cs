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
    public class MiddlewareSmsService : IMiddlewareSmsService
    {
        /*
         * Customer-wide maintenance dashboards may involve hundreds of sites.
         *
         * MMAPI is shared with other systems, so do not fire every site request
         * simultaneously. This keeps a small number of site requests in flight
         * while still avoiding completely serial dashboard loading.
         */
        private const int DashboardSiteConcurrency = 4;

        /*
         * MMAPI's system-maint-schedules endpoint performs its paging after it
         * has gathered the site's maintained-system schedules.
         *
         * A site should never realistically approach this many maintained
         * systems, so this allows us to retrieve the complete site result in
         * one MMAPI request while retaining a safety check.
         */
        private const int DashboardSitePageSize = 1000;

        private readonly IConfiguration _config;
        private readonly IMiddlewareAuthService _authService;
        private readonly HttpClient _httpClient;

        public MiddlewareSmsService(
            IConfiguration config,
            IMiddlewareAuthService authService,
            HttpClient httpClient)
        {
            _config = config;
            _authService = authService;
            _httpClient = httpClient;
        }

        // =========================================================
        // EXISTING SINGLE-SYSTEM FLOW
        // =========================================================

        public async Task<ServiceResult<PortalSMSResponse>> GetSms(
            PortalSMSQuery query,
            CancellationToken ct = default)
        {
            var cleanSiteId =
                query.SiteId?.Trim().ToUpperInvariant() ?? "";

            if (query.SystemNo <= 0)
            {
                query.SystemNo = 1;
            }

            if (string.IsNullOrWhiteSpace(cleanSiteId))
            {
                return ServiceResult<PortalSMSResponse>.Fail(
                    "Site ID is required.",
                    400);
            }

            var contextResult =
                await GetMiddlewareRequestContext(ct);

            if (!contextResult.Success ||
                contextResult.Data is null)
            {
                return ServiceResult<PortalSMSResponse>.Fail(
                    contextResult.Error ??
                        "Could not obtain middleware authentication.",
                    contextResult.StatusCode);
            }

            return await SendMaintenanceSchedulesRequest(
                contextResult.Data.BaseUrl,
                contextResult.Data.Token,
                customerNo: "",
                siteId: cleanSiteId,
                systemNo: query.SystemNo,
                nextMaintenanceFrom: null,
                nextMaintenanceTo: null,
                pageSize: 50,
                ct);
        }

        // =========================================================
        // DASHBOARD MULTI-SITE FLOW
        // =========================================================

        public async Task<ServiceResult<List<PortalSMSDto>>>
            GetMaintenanceSchedulesForSites(
                string customerNo,
                IReadOnlyCollection<string> siteIds,
                DateTime? nextMaintenanceFrom,
                DateTime? nextMaintenanceTo,
                CancellationToken ct = default)
        {
            var cleanCustomerNo =
                customerNo?.Trim().ToUpperInvariant() ?? "";

            var cleanSiteIds = siteIds
                .Select(siteId =>
                    siteId?.Trim().ToUpperInvariant() ?? "")
                .Where(siteId =>
                    !string.IsNullOrWhiteSpace(siteId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (cleanSiteIds.Count == 0)
            {
                return ServiceResult<List<PortalSMSDto>>.Ok([]);
            }

            /*
             * Get one MMAPI token for the complete dashboard batch instead of
             * authenticating separately for every site.
             */
            var contextResult =
                await GetMiddlewareRequestContext(ct);

            if (!contextResult.Success ||
                contextResult.Data is null)
            {
                return ServiceResult<List<PortalSMSDto>>.Fail(
                    contextResult.Error ??
                        "Could not obtain middleware authentication.",
                    contextResult.StatusCode);
            }

            using var concurrencyGate =
                new SemaphoreSlim(
                    DashboardSiteConcurrency,
                    DashboardSiteConcurrency);

            var tasks = cleanSiteIds
                .Select(async siteId =>
                {
                    await concurrencyGate.WaitAsync(ct);

                    try
                    {
                        try
                        {
                            /*
                             * Intentionally omit SystemNo.
                             *
                             * Existing MMAPI behaviour then:
                             *
                             * 1. loads the site's systems;
                             * 2. filters Maintained_YN = "Y";
                             * 3. gets futuremaintenanceschedules for each;
                             * 4. returns the resulting schedules.
                             */
                            var result =
                                await SendMaintenanceSchedulesRequest(
                                    contextResult.Data.BaseUrl,
                                    contextResult.Data.Token,
                                    cleanCustomerNo,
                                    siteId,
                                    systemNo: null,
                                    nextMaintenanceFrom,
                                    nextMaintenanceTo,
                                    DashboardSitePageSize,
                                    ct);

                            return new SiteMaintenanceResult(
                                siteId,
                                result);
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            return new SiteMaintenanceResult(
                                siteId,
                                ServiceResult<PortalSMSResponse>.Fail(
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

            var siteResults =
                await Task.WhenAll(tasks);

            /*
             * Do not silently return a partial dashboard if one site failed.
             * A partial total would look valid to the customer but be wrong.
             */
            var failedSite =
                siteResults.FirstOrDefault(result =>
                    !result.Result.Success);

            if (failedSite is not null)
            {
                return ServiceResult<List<PortalSMSDto>>.Fail(
                    $"Unable to retrieve maintenance schedules for site " +
                    $"'{failedSite.SiteId}'. " +
                    (failedSite.Result.Error ??
                        "Middleware maintenance schedule request failed."),
                    failedSite.Result.StatusCode);
            }

            /*
             * Each request should contain all maintained systems for one site.
             * If HasMore is true we would otherwise silently omit systems.
             */
            var oversizedSite =
                siteResults.FirstOrDefault(result =>
                    result.Result.Data?.HasMore == true);

            if (oversizedSite is not null)
            {
                return ServiceResult<List<PortalSMSDto>>.Fail(
                    $"Maintenance schedule retrieval for site " +
                    $"'{oversizedSite.SiteId}' exceeded the " +
                    $"{DashboardSitePageSize} record safety limit.",
                    502);
            }

            /*
             * Site ID + System No should uniquely identify a maintenance
             * schedule for this dashboard.
             */
            var schedules = siteResults
                .SelectMany(result =>
                    result.Result.Data?.Items ?? [])
                .GroupBy(
                    schedule =>
                        $"{schedule.SiteId?.Trim().ToUpperInvariant()}|" +
                        $"{schedule.SystemNo}",
                    StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                    group.First())
                .ToList();

            return ServiceResult<List<PortalSMSDto>>.Ok(
                schedules);
        }

        // =========================================================
        // SHARED MMAPI REQUEST
        // =========================================================

        private async Task<ServiceResult<PortalSMSResponse>>
            SendMaintenanceSchedulesRequest(
                string baseUrl,
                string token,
                string customerNo,
                string siteId,
                int? systemNo,
                DateTime? nextMaintenanceFrom,
                DateTime? nextMaintenanceTo,
                int pageSize,
                CancellationToken ct)
        {
            var parameters =
                new Dictionary<string, string?>
                {
                    ["customerNo"] =
                        string.IsNullOrWhiteSpace(customerNo)
                            ? null
                            : customerNo,

                    ["siteId"] = siteId,

                    ["systemNo"] =
                        systemNo?.ToString(),

                    ["nextMaintenanceFrom"] =
                        nextMaintenanceFrom?.ToString("O"),

                    ["nextMaintenanceTo"] =
                        nextMaintenanceTo?.ToString("O"),

                    ["page"] = "1",

                    ["pageSize"] =
                        pageSize.ToString()
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
                    $"{baseUrl}/api/system-maint-schedules?{queryString}");

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

                return ServiceResult<PortalSMSResponse>.Fail(
                    string.IsNullOrWhiteSpace(error)
                        ? "Middleware systems maintenance schedules request failed."
                        : error,
                    (int)response.StatusCode);
            }

            var middlewareResponse =
                await response.Content
                    .ReadFromJsonAsync<MiddlewareSMSResponse>(
                        cancellationToken: ct);

            if (middlewareResponse is null)
            {
                return ServiceResult<PortalSMSResponse>.Fail(
                    "Middleware returned an invalid maintenance schedules response.",
                    502);
            }

            var result =
                new PortalSMSResponse
                {
                    Items =
                        middlewareResponse.Items
                            .Select(MapSms)
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

            return ServiceResult<PortalSMSResponse>.Ok(
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
                        "Could not obtain middleware authentication.",
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

        private static PortalSMSDto MapSms(
            MiddlewareSMS sms)
        {
            return new PortalSMSDto
            {
                SiteId =
                    sms.SiteId?
                        .Trim()
                        .ToUpperInvariant()
                    ?? "",

                SystemNo =
                    sms.SystemNo < 1
                        ? 1
                        : sms.SystemNo,

                NextMaintenanceDate =
                    sms.NextMaintenanceDate ?? "",

                Description =
                    sms.Description ?? ""
            };
        }

        private sealed record MiddlewareRequestContext(
            string BaseUrl,
            string Token);

        private sealed record SiteMaintenanceResult(
            string SiteId,
            ServiceResult<PortalSMSResponse> Result);
    }
}