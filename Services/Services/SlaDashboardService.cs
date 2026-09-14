using mysystem_bff.Models.Admin;
using mysystem_bff.Models.Portal;
using mysystem_bff.Models.Portal.DashboardData;
using mysystem_bff.Services.Dashboard;
using mysystem_bff.Services.Helpers;
using mysystem_bff.Services.Interfaces;

namespace mysystem_bff.Services.Services;

public class SlaDashboardService : ISlaDashboardService
{
    private readonly IMiddlewareCallsService _callsService;

    public SlaDashboardService(
        IMiddlewareCallsService callsService)
    {
        _callsService = callsService;
    }

    // =========================================================
    // SLA DASHBOARD SUMMARY
    // =========================================================

    public async Task<ServiceResult<PortalSlaDashboardDataDto>>
        GetDashboardDataAsync(
            PortalDashboardDataQuery query,
            CancellationToken ct = default)
    {
        var customerNo =
            query.CustomerNo?
                .Trim()
                .ToUpperInvariant()
            ?? "";

        var siteId =
            query.SiteId?
                .Trim()
                .ToUpperInvariant()
            ?? "";

        if (string.IsNullOrWhiteSpace(customerNo))
        {
            return ServiceResult<PortalSlaDashboardDataDto>.Fail(
                "Customer No is required for SLA dashboard data.",
                400);
        }

        if (!SlaCustomerHelper.HasSla(customerNo))
        {
            return ServiceResult<PortalSlaDashboardDataDto>.Fail(
                "SLA dashboard data is not available for this customer.",
                400);
        }

        if (!DashboardDateHelper.IsValidYear(
            query.DataYear))
        {
            return ServiceResult<PortalSlaDashboardDataDto>.Fail(
                "A valid data year is required.",
                400);
        }

        var (loggedFrom, loggedTo) =
            DashboardDateHelper.GetDateRange(
                query.DataMonth,
                query.DataYear);

        var callsResult =
            await GetAllCalls(
                customerNo,
                siteId,
                loggedFrom,
                loggedTo,
                ct);

        if (!callsResult.Success)
        {
            return ServiceResult<PortalSlaDashboardDataDto>.Fail(
                callsResult.Error ??
                    "Unable to retrieve calls for SLA dashboard.",
                callsResult.StatusCode);
        }

        var slaCalls =
            GetSlaApplicableCalls(
                callsResult.Data ?? []);

        var successfulCalls =
            slaCalls.Count(call =>
                !HasFailedSla(call));

        var failedCalls =
            slaCalls.Count(
                HasFailedSla);

        var totalCalls =
            slaCalls.Count;

        var successPercentage =
            totalCalls == 0
                ? 0m
                : Math.Round(
                    (decimal)successfulCalls /
                    totalCalls *
                    100m,
                    1);

        var failurePercentage =
            totalCalls == 0
                ? 0m
                : Math.Round(
                    (decimal)failedCalls /
                    totalCalls *
                    100m,
                    1);

        var result =
            new PortalSlaDashboardDataDto
            {
                CustomerNo =
                    customerNo,

                SiteId =
                    siteId,

                TotalCalls =
                    totalCalls,

                SuccessfulCalls =
                    successfulCalls,

                FailedCalls =
                    failedCalls,

                SuccessPercentage =
                    successPercentage,

                FailurePercentage =
                    failurePercentage,

                SlaBreakdown =
                [
                    new DashboardBreakdownItemDto
                    {
                        Code =
                            "SUCCESSFUL",

                        Label =
                            "Within SLA",

                        Count =
                            successfulCalls
                    },

                    new DashboardBreakdownItemDto
                    {
                        Code =
                            "FAILED",

                        Label =
                            "Breached SLA",

                        Count =
                            failedCalls
                    }
                ]
            };

        return ServiceResult<
            PortalSlaDashboardDataDto>
            .Ok(result);
    }

    // =========================================================
    // SLA DASHBOARD SUPPORT ITEMS
    // =========================================================

    public async Task<ServiceResult<PortalDashboardSlaItemsResponse>>
        GetDashboardItemsAsync(
            PortalDashboardSlaItemsQuery query,
            CancellationToken ct = default)
    {
        var customerNo =
            query.CustomerNo?
                .Trim()
                .ToUpperInvariant()
            ?? "";

        var siteId =
            query.SiteId?
                .Trim()
                .ToUpperInvariant()
            ?? "";

        if (string.IsNullOrWhiteSpace(customerNo))
        {
            return ServiceResult<PortalDashboardSlaItemsResponse>.Fail(
                "Customer No is required for SLA dashboard data.",
                400);
        }

        if (!SlaCustomerHelper.HasSla(customerNo))
        {
            return ServiceResult<PortalDashboardSlaItemsResponse>.Fail(
                "SLA dashboard data is not available for this customer.",
                400);
        }

        if (!DashboardDateHelper.IsValidYear(
            query.DataYear))
        {
            return ServiceResult<PortalDashboardSlaItemsResponse>.Fail(
                "A valid data year is required.",
                400);
        }

        var page =
            query.Page > 0
                ? query.Page
                : 1;

        var pageSize =
            Math.Clamp(
                query.PageSize,
                1,
                30);

        var (loggedFrom, loggedTo) =
            DashboardDateHelper.GetDateRange(
                query.DataMonth,
                query.DataYear);

        var callsResult =
            await GetAllCalls(
                customerNo,
                siteId,
                loggedFrom,
                loggedTo,
                ct);

        if (!callsResult.Success)
        {
            return ServiceResult<PortalDashboardSlaItemsResponse>.Fail(
                callsResult.Error ??
                    "Unable to retrieve calls for SLA dashboard.",
                callsResult.StatusCode);
        }

        var slaCalls =
            GetSlaApplicableCalls(
                callsResult.Data ?? []);

        var filteredCalls =
            slaCalls
                .Where(call =>
                    MatchesSlaFilter(
                        call,
                        query.FilterType))
                .OrderByDescending(call =>
                    call.CallNumber)
                .ToList();

        var total =
            filteredCalls.Count;

        var items =
            filteredCalls
                .Skip(
                    (page - 1) *
                    pageSize)
                .Take(pageSize)
                .ToList();

        var result =
            new PortalDashboardSlaItemsResponse
            {
                Items =
                    items,

                Page =
                    page,

                PageSize =
                    pageSize,

                Total =
                    total,

                HasMore =
                    page * pageSize < total
            };

        return ServiceResult<
            PortalDashboardSlaItemsResponse>
            .Ok(result);
    }

    // =========================================================
    // RETRIEVE ALL CALLS
    // =========================================================

    private async Task<ServiceResult<List<PortalCallDto>>>
        GetAllCalls(
            string customerNo,
            string siteId,
            DateTime loggedFrom,
            DateTime loggedTo,
            CancellationToken ct)
    {
        var allCalls =
            new List<PortalCallDto>();

        var page =
            1;

        var hasMore =
            true;

        const int maximumPages =
            500;

        while (
            hasMore &&
            page <= maximumPages)
        {
            var callsQuery =
                new PortalCallsQuery
                {
                    CustomerNo =
                        customerNo,

                    SiteId =
                        siteId,

                    LoggedFrom =
                        loggedFrom,

                    LoggedTo =
                        loggedTo,

                    Page =
                        page,

                    PageSize =
                        100
                };

            var result =
                await _callsService.GetCalls(
                    callsQuery,
                    ct);

            if (!result.Success)
            {
                return ServiceResult<List<PortalCallDto>>.Fail(
                    result.Error ??
                        "Unable to retrieve calls from middleware.",
                    result.StatusCode);
            }

            if (result.Data is null)
            {
                return ServiceResult<List<PortalCallDto>>.Fail(
                    "Middleware returned no call data.",
                    502);
            }

            allCalls.AddRange(
                result.Data.Items);

            hasMore =
                result.Data.HasMore;

            page++;
        }

        if (hasMore)
        {
            return ServiceResult<List<PortalCallDto>>.Fail(
                "SLA dashboard call retrieval exceeded the maximum page limit.",
                502);
        }

        return ServiceResult<
            List<PortalCallDto>>
            .Ok(allCalls);
    }

    // =========================================================
    // SLA FILTERING
    // =========================================================

    private static List<PortalCallDto>
        GetSlaApplicableCalls(
            IEnumerable<PortalCallDto> calls)
    {
        return calls
            .Where(call =>
                IsStatus(
                    call.CallStatus,
                    "C"))
            .Where(call =>
                !IsMaintenanceCall(
                    call.CallType))
            .ToList();
    }

    private static bool HasFailedSla(
        PortalCallDto call)
    {
        return string.Equals(
            CleanCode(
                call.FailedToRespond_YN),
            "Y",
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesSlaFilter(
        PortalCallDto call,
        DashboardSlaFilterType filterType)
    {
        return filterType switch
        {
            DashboardSlaFilterType.ALL =>
                true,

            DashboardSlaFilterType.SUCCESSFUL =>
                !HasFailedSla(call),

            DashboardSlaFilterType.FAILED =>
                HasFailedSla(call),

            _ =>
                false
        };
    }

    // =========================================================
    // GENERAL HELPERS
    // =========================================================

    private static bool IsMaintenanceCall(
        string? callType)
    {
        return string.Equals(
            CleanCode(callType),
            "P",
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStatus(
        string? status,
        string expected)
    {
        return string.Equals(
            CleanCode(status),
            CleanCode(expected),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string CleanCode(
        string? value)
    {
        return value?
            .Trim()
            .ToUpperInvariant()
            ?? "";
    }
}