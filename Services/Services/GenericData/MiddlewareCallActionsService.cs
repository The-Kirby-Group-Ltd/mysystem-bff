using mysystem_bff.Models.Admin;
using mysystem_bff.Models.Middleware.Calls;
using mysystem_bff.Models.Portal;
using mysystem_bff.Models.Portal.Queries;
using mysystem_bff.Models.Portal.Responses;
using mysystem_bff.Services.Interfaces.GenericData;
using mysystem_bff.Services.Interfaces.Security;
using System.Net.Http.Headers;

namespace mysystem_bff.Services.Services.GenericData
{
    public class MiddlewareCallActionsService : IMiddlewareCallActionsService
    {
        private readonly HttpClient _httpClient;
        private readonly IMiddlewareAuthService _authService;
        private readonly IConfiguration _config;

        public MiddlewareCallActionsService (
            HttpClient httpClient, IMiddlewareAuthService authService, IConfiguration config)
        {
            _httpClient = httpClient;
            _authService = authService;
            _config = config;
        }

        // ===========================================
        // super function
        // ===========================================

        public async Task<ServiceResult<PortalCallActionsResponse>> GetCallActions(
            PortalCallActionsQuery query,
            CancellationToken ct)
        {
            var callNumber = query.CallNumber;
            var actionNumber = query.ActionNo;
            var cleanEngineer = query.Engineer?.Trim() ?? "";

            // bad request check
            if (callNumber <= 0)
            {
                return ServiceResult<PortalCallActionsResponse>.Fail(
                    "A valid call number could not be resolved from the request.",
                    400);
            }

            // get mmapi token
            var tokenResult = await _authService.GetMiddlewareToken();

            // cannot get token
            if (!tokenResult.Success ||
                string.IsNullOrWhiteSpace(tokenResult.Data))
            {
                return ServiceResult<PortalCallActionsResponse>.Fail(
                    tokenResult.Error ?? "Failed to authenticate to Kirby API",
                    tokenResult.StatusCode);
            }

            // mmapi base url
            var baseUrl = _config["MiddlewareApi:BaseUrl"];

            // cannot get base url
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return ServiceResult<PortalCallActionsResponse>.Fail(
                "API base URL is missing.",
                500);
            }

            // normalise pagination
            var page = query.Page > 0 ? query.Page : 1;
            var pageSize = Math.Clamp(query.PageSize, 1, 50);

            // set up parameters for mmapi request
            var parameters = new Dictionary<string, string?>
            {
                ["callNumber"] = callNumber.ToString(),
                ["actionNo"] = actionNumber > 0
                    ? actionNumber.ToString()
                    : null,
                ["engineer"] = cleanEngineer,
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

            // set up http request
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{baseUrl.TrimEnd('/')}/api/call-actions?{queryString}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.Data);

            using var response = await _httpClient.SendAsync(request, ct);

            // failed mmapi response
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);

                return ServiceResult<PortalCallActionsResponse>.Fail(
                    string.IsNullOrWhiteSpace(error)
                        ? "Middleware call actions request failed."
                        : error,
                    (int)response.StatusCode);
            }

            // json response conversion
            var middlewareResponse =
                await response.Content
                    .ReadFromJsonAsync<MiddlewareCallActionsResponse>(
                        cancellationToken: ct);

            // failed to parse to json
            if (middlewareResponse is null)
            {
                return ServiceResult<PortalCallActionsResponse>.Fail(
                    "Data could not be converted to Json.",
                    500);
            }

            var result = new PortalCallActionsResponse
            {
                Items = middlewareResponse.Items.Select(MapCallAction).ToList(),
                Page = middlewareResponse.Page,
                PageSize = middlewareResponse.PageSize,
                Total = middlewareResponse.Total,
                HasMore = middlewareResponse.HasMore
            };

            return ServiceResult<PortalCallActionsResponse>.Ok(result);
        }

        // ===========================================
        // map middleware object to portal object
        // ===========================================

        private static PortalCallActionDto MapCallAction(MiddlewareCallAction action)
        {
            return new PortalCallActionDto
            {
                CallNumber = action.CallNumber,
                CallActionNumber = action.CallActionNumber, 
                Remarks = action.Remarks,
                AppointmentDate = action.AppointmentDate,
                AppointmentFromTime = action.AppointmentFromTime,
                StartedDate = action.StartedDate,
                StartedTime = action.StartedTime,
                FinishedDate = action.FinishedDate,
                FinishedTime = action.FinishedTime,
                HoursOnSite = action.HoursOnSite,
                MinutesOnSite = action.MinutesOnSite,
                Engineer = action.Engineer,
                ActionTaken = action.ActionTaken,
                SignatureName = action.SignatureName,
                OnCallEngineersName = action.OnCallEngineersName,
                OnRouteDate = action.OnRouteDate,
                OnRouteTime = action.OnRouteTime,
                OnSiteDate = action.OnSiteDate,
                OnSiteTime = action.OnSiteTime,
                SLADeadlineDate = action.SLADeadlineDate,
                SLADeadlineTime = action.SLADeadlineTime,
                SLAStartDate = action.SLAStartDate,
                SLAStartTime = action.SLAStartTime,
                OvertimeType = action.OvertimeType,
                OvertimeStartDate = action.OvertimeStartDate,
                OvertimeStartTime = action.OvertimeStartTime,
                OvertimeFinishDate = action.OvertimeFinishDate,
                OvertimeFinishTime = action.OvertimeFinishTime,
                RemoteFix_YN = action.RemoteFix_YN,
                PropertyReferenceNo = action.PropertyReferenceNo,
                Name = action.Name,
                CallStatus = action.CallStatus,
                CustomerReference = action.CustomerReference,
                SiteName = action.SiteName
            };
        }
    }
}
