using mysystem_bff.Models.Admin;
using mysystem_bff.Models.Middleware;
using mysystem_bff.Models.Middleware.Reference;
using mysystem_bff.Models.Portal.Reference;
using mysystem_bff.Models.Portal.Responses;
using mysystem_bff.Services.Interfaces.GenericData;
using mysystem_bff.Services.Interfaces.Security;
using System.Net;
using System.Net.Http.Headers;

namespace mysystem_bff.Services.Services.GenericData;

public class MiddlewareReferenceService : IMiddlewareReferenceService
{
    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;
    private readonly IMiddlewareAuthService _authService;

    public MiddlewareReferenceService(
        IConfiguration config,
        HttpClient httpClient,
        IMiddlewareAuthService authService)
    {
        _config = config;
        _httpClient = httpClient;
        _authService = authService;
    }

    public async Task<
        ServiceResult<PortalPagedResponse<PortalSystemTypeDto>>
    > GetSystemTypes(
        PortalReferenceQuery query,
        CancellationToken ct = default)
    {
        var result = await GetReferenceData<
            MiddlewareSystemType,
            PortalSystemTypeDto>(
                "/api/cf-system-types",
                query,
                MapSystemType,
                ct);

        return result;
    }

    public async Task<
        ServiceResult<PortalPagedResponse<PortalEngineerDto>>
    > GetEngineers(
        PortalReferenceQuery query,
        CancellationToken ct = default)
    {
        var result = await GetReferenceData<
            MiddlewareEngineer,
            PortalEngineerDto>(
                "/api/cf-engineers",
                query,
                MapEngineer,
                ct);

        return result;
    }

    private async Task<
        ServiceResult<PortalPagedResponse<TPortal>>
    > GetReferenceData<TMiddleware, TPortal>(
        string endpoint,
        PortalReferenceQuery query,
        Func<TMiddleware, TPortal> mapItem,
        CancellationToken ct)
    {
        var tokenResult =
            await _authService.GetMiddlewareToken();

        if (!tokenResult.Success ||
            string.IsNullOrWhiteSpace(tokenResult.Data))
        {
            return ServiceResult<
                PortalPagedResponse<TPortal>
            >.Fail(
                tokenResult.Error ??
                "Unable to authenticate with middleware API.",
                tokenResult.StatusCode);
        }

        var baseUrl = _config["MiddlewareApi:BaseUrl"];

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return ServiceResult<
                PortalPagedResponse<TPortal>
            >.Fail(
                "Middleware API base URL is missing.",
                500);
        }

        var page = query.Page > 0
            ? query.Page
            : 1;

        var pageSize = Math.Clamp(
            query.PageSize,
            1,
            100);

        var parameters = new Dictionary<string, string?>
        {
            ["code"] = query.Code?
                .Trim()
                .ToUpperInvariant(),

            ["description"] = query.Description?
                .Trim(),

            ["page"] = page.ToString(),
            ["pageSize"] = pageSize.ToString()
        };

        var queryString = string.Join(
            "&",
            parameters
                .Where(parameter =>
                    !string.IsNullOrWhiteSpace(
                        parameter.Value))
                .Select(parameter =>
                    $"{Uri.EscapeDataString(parameter.Key)}=" +
                    $"{Uri.EscapeDataString(parameter.Value!)}"));

        var cleanBaseUrl = baseUrl.TrimEnd('/');

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{cleanBaseUrl}{endpoint}?{queryString}");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                tokenResult.Data);

        using var response = await _httpClient.SendAsync(
            request,
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content
                .ReadAsStringAsync(ct);

            return ServiceResult<
                PortalPagedResponse<TPortal>
            >.Fail(
                string.IsNullOrWhiteSpace(error)
                    ? "Middleware reference request failed."
                    : error,
                (int)response.StatusCode);
        }

        var middlewareResponse =
            await response.Content
                .ReadFromJsonAsync<
                    MiddlewarePagedResponse<TMiddleware>
                >(
                    cancellationToken: ct);

        if (middlewareResponse is null)
        {
            return ServiceResult<
                PortalPagedResponse<TPortal>
            >.Fail(
                "Middleware API returned an invalid reference response.",
                502);
        }

        var portalResponse =
            new PortalPagedResponse<TPortal>
            {
                Items = middlewareResponse.Items
                    .Select(mapItem)
                    .ToList(),

                Page = middlewareResponse.Page,
                PageSize = middlewareResponse.PageSize,
                Total = middlewareResponse.Total,
                HasMore = middlewareResponse.HasMore
            };

        return ServiceResult<
            PortalPagedResponse<TPortal>
        >.Ok(portalResponse);
    }

    private static PortalSystemTypeDto MapSystemType(
        MiddlewareSystemType systemType)
    {
        return new PortalSystemTypeDto
        {
            Code = systemType.Code ?? "",
            Description = systemType.Description ?? ""
        };
    }

    private static PortalEngineerDto MapEngineer(
        MiddlewareEngineer engineer)
    {
        return new PortalEngineerDto
        {
            Code = engineer.Code ?? "",
            Description = engineer.Description ?? "",
            Status = engineer.Status ?? "",
            Telephone = engineer.Telephone ?? "",
            Email = engineer.EMail ?? ""
        };
    }

    // =========================================================
    // Failed to respond reason
    // =========================================================

    public async Task<ServiceResult<PortalFailedToRespondReasonDto>>
        GetFailedToRespondReason(
            string code,
            CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return ServiceResult<PortalFailedToRespondReasonDto>.Fail(
                "Failed-to-respond reason code is required.",
                400);
        }

        var cleanCode =
            code.Trim().ToUpperInvariant();

        var baseUrl =
            _config["MiddlewareApi:BaseUrl"];

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return ServiceResult<PortalFailedToRespondReasonDto>.Fail(
                "Middleware API configuration is missing.",
                500);
        }

        var tokenResult =
            await _authService.GetMiddlewareToken();

        if (
            !tokenResult.Success ||
            string.IsNullOrWhiteSpace(tokenResult.Data)
        )
        {
            return ServiceResult<PortalFailedToRespondReasonDto>.Fail(
                tokenResult.Error ??
                "Failed to authenticate with middleware API.",
                tokenResult.StatusCode > 0
                    ? tokenResult.StatusCode
                    : 502);
        }

        var url =
            $"{baseUrl.TrimEnd('/')}/api/reference/failed-to-respond-reasons/{Uri.EscapeDataString(cleanCode)}";

        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                url);

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                tokenResult.Data);

        using var response =
            await _httpClient.SendAsync(
                request,
                ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return ServiceResult<PortalFailedToRespondReasonDto>.Fail(
                "Failed-to-respond reason not found.",
                404);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody =
                await response.Content.ReadAsStringAsync(ct);

            return ServiceResult<PortalFailedToRespondReasonDto>.Fail(
                $"Middleware API request failed. Status={(int)response.StatusCode}. Body={errorBody}",
                502);
        }

        var result =
            await response.Content.ReadFromJsonAsync<MiddlewareFailedToRespondReason>(
                cancellationToken: ct);

        if (result is null)
        {
            return ServiceResult<PortalFailedToRespondReasonDto>.Fail(
                "Middleware API returned an invalid failed-to-respond reason response.",
                502);
        }

        var portalDto =
            new PortalFailedToRespondReasonDto
            {
                Code = result.Code,
                Description = result.Description
            };

        return ServiceResult<PortalFailedToRespondReasonDto>.Ok(
            portalDto);
    }
}