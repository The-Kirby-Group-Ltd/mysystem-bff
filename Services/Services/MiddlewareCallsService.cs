namespace mysystem_bff.Services.Services;

using mysystem_bff.Models.Admin;
using mysystem_bff.Models.Middleware;
using mysystem_bff.Models.Portal;
using mysystem_bff.Services.Interfaces;
using System.Net.Http.Headers;
using System.Net.Http.Json;

public class MiddlewareCallsService : IMiddlewareCallsService
{
    private readonly HttpClient _httpClient;
    private readonly IMiddlewareAuthService _authService;
    private readonly IConfiguration _config;

    public MiddlewareCallsService(
        HttpClient httpClient, 
        IMiddlewareAuthService authService,
        IConfiguration config)
    {
        _httpClient = httpClient;
        _authService = authService;
        _config = config;
    }

    public async Task<ServiceResult<PortalCallsResponse>> GetCalls(
        PortalCallsQuery query,
        CancellationToken cancellationToken = default)
    {
        var customerNo = query.CustomerNo?.Trim().ToUpperInvariant() ?? "";
        var siteId = query.SiteId?.Trim().ToUpperInvariant() ?? "";
        var callNumber = (query.CallNumber > 0) ? query.CallNumber : 0;
        var callNumberAsString = callNumber.ToString();

        // ensure minimum data
        if (string.IsNullOrWhiteSpace(customerNo) && string.IsNullOrWhiteSpace(siteId) && callNumber == 0)
        {
            return ServiceResult<PortalCallsResponse>.Fail(
                "Customer No, Site ID or Call Number are Required.",
                400);
        }

        // get token
        var tokenResult = await _authService.GetMiddlewareToken();

        // cannot get token
        if (!tokenResult.Success ||
            string.IsNullOrWhiteSpace(tokenResult.Data))
        {
            return ServiceResult<PortalCallsResponse>.Fail(
                tokenResult.Error ?? "Failed to authenticate to Kirby API",
                tokenResult.StatusCode);
        }

        // get baseurl
        var baseUrl = _config["MiddlewareApi:BaseUrl"];

        // cannot get base url
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return ServiceResult<PortalCallsResponse>.Fail(
            "API base URL is missing.",
            500);
        }

        // call number was given -> retrieve one call
        if (callNumber > 0)
        {
            var call = await GetCallByNumber(
                tokenResult.Data, 
                callNumberAsString, 
                baseUrl, 
                cancellationToken);

            return call;
        }

        // normalise pagination
        var page = query.Page > 0 ? query.Page : 1;
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        // set query parameters
        var parameters = new Dictionary<string, string?>
        {
            ["customerNo"] = customerNo,
            ["siteId"] = siteId,
            ["engineer"] = query.Engineer?.Trim().ToUpperInvariant(),
            ["systemType"] = query.SystemType?.Trim().ToUpperInvariant(),
            ["loggedFrom"] = query.LoggedFrom?.ToString("yyyy-MM-dd"),
            ["loggedTo"] = query.LoggedTo?.ToString("yyyy-MM-dd"),
            ["page"] = page.ToString(),
            ["pageSize"] = pageSize.ToString()
        };

        // set a query string with active params
        var queryString = string.Join(
            "&",
            parameters
                .Where(parameter =>
                    !string.IsNullOrWhiteSpace(parameter.Value))
                .Select(parameter =>
                    $"{Uri.EscapeDataString(parameter.Key)}=" +
                    $"{Uri.EscapeDataString(parameter.Value!)}"));

        // set up mmapi request
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{baseUrl}/api/calls?{queryString}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.Data);

        // send mmapi request
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        // failed mmapi request
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);

            return ServiceResult<PortalCallsResponse>.Fail(
                string.IsNullOrWhiteSpace(error)
                    ? "Middleware calls request failed."
                    : error,
                (int)response.StatusCode);
        }

        // json response conversion
        var middlewareResponse =
            await response.Content
                .ReadFromJsonAsync<MiddlewareCallsResponse>(
                    cancellationToken: cancellationToken);

        // failed to parse to json
        if (middlewareResponse is null)
        {
            return ServiceResult<PortalCallsResponse>.Fail(
                "Data could not be converted to Json.",
                500);
        }

        var result = new PortalCallsResponse
        {
            Items = middlewareResponse.Items.Select(MapCall).ToList(),
            Page = middlewareResponse.Page,
            PageSize = middlewareResponse.PageSize,
            Total = middlewareResponse.Total,
            HasMore = middlewareResponse.HasMore
        };

        return ServiceResult<PortalCallsResponse>.Ok(result);
    }

    // get singular call
    private async Task<ServiceResult<PortalCallsResponse>> GetCallByNumber(
        string token,
        string callNumber,
        string baseUrl,
        CancellationToken cancellationToken)
    {
        // set up singular call request
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{baseUrl}/api/calls?callNumber={callNumber}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // send request
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        // failed mmapi request
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            return ServiceResult<PortalCallsResponse>.Fail(
                string.IsNullOrWhiteSpace(error)
                    ? "Middleware calls request failed."
                    : error,
                (int)response.StatusCode);
        }

        // convert to json
        var middlewareResponse =
            await response.Content
            .ReadFromJsonAsync<MiddlewareCallsResponse>(
                cancellationToken: cancellationToken);

        if (middlewareResponse is null)
        {
            return ServiceResult<PortalCallsResponse>.Fail(
                "Data could not be converted to Json.",
                500);
        }

        var result = new PortalCallsResponse
        {
            Items = middlewareResponse.Items.Select(MapCall).ToList(),
            Page = middlewareResponse.Page,
            PageSize = middlewareResponse.PageSize,
            Total = middlewareResponse.Total,
            HasMore = middlewareResponse.HasMore
        };

        return ServiceResult<PortalCallsResponse>.Ok(result);
    }

    // map from middleware response to backend model 
    private static PortalCallDto MapCall(MiddlewareCall call)
    {
        return new PortalCallDto
        {
            CallNumber = call.CallNumber,
            CallType = call.CallType,
            CallStatus = call.CallStatus,
            SiteId = call.SiteID ?? "",
            LoggedDate = call.LoggedDate,
            LoggingOperator = call.LoggingOperator,
            Engineer = call.Engineer,
            SystemType = call.SystemType,
            CompletedDate = call.CompletedDate,
            CustomerReference = call.CustomerReference,
            InvoiceNo = call.InvoiceNo,
            LoggedRemarks = call.LoggedRemarks,
            PreviousMaintenanceDate = call.PreviousMaintenanceDate,
            NextMaintenanceDate = call.NextMaintenanceDate,
            FailedToRespond_YN = call.FailedToRespond_YN,
            FailedToRespondReason = call.FailedToRespondReason,
        };
    }
}