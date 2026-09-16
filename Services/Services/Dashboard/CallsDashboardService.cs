namespace mysystem_bff.Services.Services.Dashboard;

using mysystem_bff.Models.Admin;

using mysystem_bff.Models.Portal;
using mysystem_bff.Models.Portal.DashboardData;
using mysystem_bff.Models.Portal.Queries;
using mysystem_bff.Models.Portal.Reference;

using mysystem_bff.Services.Dashboard;
using mysystem_bff.Services.Interfaces.Dashboard;
using mysystem_bff.Services.Interfaces.GenericData;

public class CallsDashboardService
    : ICallsDashboardService
{
    private readonly IMiddlewareCallsService _callsService;
    private readonly IMiddlewareReferenceService _referenceService;

    public CallsDashboardService(
        IMiddlewareCallsService callsService,
        IMiddlewareReferenceService referenceService)
    {
        _callsService = callsService;
        _referenceService = referenceService;
    }

    // =========================================================
    // CALLS DASHBOARD SUMMARY
    // =========================================================

    public async Task<ServiceResult<PortalCallsDashboardDataDto>>
        GetDashboardDataAsync(
            PortalDashboardDataQuery query,
            CancellationToken ct = default)
    {
        var customerNo =
            query.CustomerNo?.Trim().ToUpperInvariant() ?? "";

        var siteId =
            query.SiteId?.Trim().ToUpperInvariant() ?? "";

        if (string.IsNullOrWhiteSpace(customerNo) &&
            string.IsNullOrWhiteSpace(siteId))
        {
            return ServiceResult<PortalCallsDashboardDataDto>.Fail(
                "Either Customer No or Site ID is required.",
                400);
        }

        if (!DashboardDateHelper.IsValidYear(
            query.DataYear))
        {
            return ServiceResult<PortalCallsDashboardDataDto>.Fail(
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
            return ServiceResult<PortalCallsDashboardDataDto>.Fail(
                callsResult.Error ??
                    "Unable to retrieve calls for dashboard.",
                callsResult.StatusCode);
        }

        var activeCalls =
            RemoveCancelledCalls(
                callsResult.Data ?? []);

        // =====================================================
        // KPI DATA
        // =====================================================

        var openCalls =
            activeCalls.Count(call =>
                IsOpenStatus(
                    call.CallStatus));

        var completedCalls =
            activeCalls.Count(call =>
                IsStatus(
                    call.CallStatus,
                    "C"));

        var furtherActions =
            activeCalls.Count(call =>
                IsStatus(
                    call.CallStatus,
                    "F"));

        // =====================================================
        // STATUS BREAKDOWN
        // =====================================================

        var statusBreakdown =
            activeCalls
                .Where(call =>
                    !string.IsNullOrWhiteSpace(
                        call.CallStatus))
                .GroupBy(call =>
                    call.CallStatus!
                        .Trim()
                        .ToUpperInvariant())
                .Select(group =>
                    new DashboardBreakdownItemDto
                    {
                        Code =
                            group.Key,

                        Label =
                            GetCallStatusLabel(
                                group.Key),

                        Count =
                            group.Count()
                    })
                .OrderByDescending(item =>
                    item.Count)
                .ToList();

        // =====================================================
        // CALL TYPE BREAKDOWN
        // =====================================================

        var callTypeBreakdown =
            BuildCallTypeBreakdown(
                activeCalls);

        // =====================================================
        // SYSTEM TYPE BREAKDOWN
        // =====================================================

        var systemCategoryMapResult =
            await GetSystemCategoryMap(
                ct);

        if (!systemCategoryMapResult.Success)
        {
            return ServiceResult<PortalCallsDashboardDataDto>.Fail(
                systemCategoryMapResult.Error ??
                    "Unable to retrieve system reference data.",
                systemCategoryMapResult.StatusCode);
        }

        var systemTypeBreakdown =
            BuildSystemTypeBreakdown(
                activeCalls,
                systemCategoryMapResult.Data ?? []);

        // =====================================================
        // RESULT
        // =====================================================

        var result =
            new PortalCallsDashboardDataDto
            {
                CustomerNo =
                    customerNo,

                SiteId =
                    siteId,

                OpenCalls =
                    openCalls,

                CompletedCalls =
                    completedCalls,

                FurtherActions =
                    furtherActions,

                StatusBreakdown =
                    statusBreakdown,

                CallTypeBreakdown =
                    callTypeBreakdown,

                SystemTypeBreakdown =
                    systemTypeBreakdown
            };

        return ServiceResult<PortalCallsDashboardDataDto>.Ok(
            result);
    }

    // =========================================================
    // SUPPORTING CALL ITEMS
    // =========================================================

    public async Task<ServiceResult<PortalDashboardCallsItemsResponse>>
        GetDashboardItemsAsync(
            PortalDashboardCallsItemsQuery query,
            CancellationToken ct = default)
    {
        var customerNo =
            query.CustomerNo?.Trim().ToUpperInvariant() ?? "";

        var siteId =
            query.SiteId?.Trim().ToUpperInvariant() ?? "";

        var filterValue =
            query.FilterValue?.Trim().ToUpperInvariant() ?? "";

        if (string.IsNullOrWhiteSpace(customerNo) &&
            string.IsNullOrWhiteSpace(siteId))
        {
            return ServiceResult<PortalDashboardCallsItemsResponse>.Fail(
                "Either Customer No or Site ID is required.",
                400);
        }

        if (!DashboardDateHelper.IsValidYear(
            query.DataYear))
        {
            return ServiceResult<PortalDashboardCallsItemsResponse>.Fail(
                "A valid data year is required.",
                400);
        }

        if (RequiresFilterValue(
                query.FilterType) &&
            string.IsNullOrWhiteSpace(
                filterValue))
        {
            return ServiceResult<PortalDashboardCallsItemsResponse>.Fail(
                "A dashboard filter value is required.",
                400);
        }

        var page =
            query.Page > 0
                ? query.Page
                : 1;

        // Hard dashboard limit.
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
            return ServiceResult<PortalDashboardCallsItemsResponse>.Fail(
                callsResult.Error ??
                    "Unable to retrieve calls for dashboard.",
                callsResult.StatusCode);
        }

        var activeCalls =
            RemoveCancelledCalls(
                callsResult.Data ?? []);

        Dictionary<string, string>? systemCategoryMap =
            null;

        if (query.FilterType ==
            DashboardCallsFilterType.SYSTEM_TYPE)
        {
            var categoryMapResult =
                await GetSystemCategoryMap(
                    ct);

            if (!categoryMapResult.Success)
            {
                return ServiceResult<PortalDashboardCallsItemsResponse>.Fail(
                    categoryMapResult.Error ??
                        "Unable to retrieve system reference data.",
                    categoryMapResult.StatusCode);
            }

            systemCategoryMap =
                categoryMapResult.Data ?? [];
        }

        var filteredCalls =
            activeCalls
                .Where(call =>
                    MatchesDashboardFilter(
                        call,
                        query.FilterType,
                        filterValue,
                        systemCategoryMap))
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
                .Take(
                    pageSize)
                .ToList();

        var result =
            new PortalDashboardCallsItemsResponse
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

        return ServiceResult<PortalDashboardCallsItemsResponse>.Ok(
            result);
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
                "Dashboard call retrieval exceeded the maximum page limit.",
                502);
        }

        return ServiceResult<List<PortalCallDto>>.Ok(
            allCalls);
    }

    // =========================================================
    // SYSTEM REFERENCE LOOKUP
    // =========================================================

    private async Task<ServiceResult<Dictionary<string, string>>>
        GetSystemCategoryMap(
            CancellationToken ct)
    {
        var result =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        var page =
            1;

        var hasMore =
            true;

        while (hasMore)
        {
            var referenceResult =
                await _referenceService.GetSystemTypes(
                    new PortalReferenceQuery
                    {
                        Page =
                            page,

                        PageSize =
                            100
                    },
                    ct);

            if (!referenceResult.Success)
            {
                return ServiceResult<Dictionary<string, string>>.Fail(
                    referenceResult.Error ??
                        "Unable to retrieve system reference data.",
                    referenceResult.StatusCode);
            }

            if (referenceResult.Data is null)
            {
                return ServiceResult<Dictionary<string, string>>.Fail(
                    "System reference response was empty.",
                    502);
            }

            foreach (
                var systemType
                in referenceResult.Data.Items)
            {
                var code =
                    systemType.Code?
                        .Trim()
                        .ToUpperInvariant()
                    ?? "";

                if (string.IsNullOrWhiteSpace(
                    code))
                {
                    continue;
                }

                result[code] =
                    GetSystemCategoryCode(
                        GetSystemCategory(
                            systemType.Description ?? ""));
            }

            hasMore =
                referenceResult.Data.HasMore;

            page++;
        }

        return ServiceResult<Dictionary<string, string>>.Ok(
            result);
    }

    // =========================================================
    // STATUS BREAKDOWN
    // =========================================================

    private static string GetCallStatusLabel(
        string status)
    {
        return status
            .Trim()
            .ToUpperInvariant()
            switch
        {
            "A" =>
                "Assigned to Engineer",

            "C" =>
                "Completed",

            "E" =>
                "Engineer Completed",

            "F" =>
                "Further Action Required",

            "N" =>
                "New",

            "R" =>
                "Response in Progress",

            _ =>
                "Unknown"
        };
    }

    // =========================================================
    // CALL TYPE BREAKDOWN
    // =========================================================

    private static List<DashboardBreakdownItemDto>
        BuildCallTypeBreakdown(
            List<PortalCallDto> calls)
    {
        var ppm =
            calls.Count(call =>
                IsPpmCallType(
                    call.CallType));

        var projects =
            calls.Count(call =>
                IsProjectCallType(
                    call.CallType));

        var reactive =
            calls.Count(call =>
                IsReactiveCallType(
                    call.CallType));

        return
        [
            new DashboardBreakdownItemDto
            {
                Code =
                    "PPM",

                Label =
                    "PPM",

                Count =
                    ppm
            },

            new DashboardBreakdownItemDto
            {
                Code =
                    "PROJECTS",

                Label =
                    "Projects",

                Count =
                    projects
            },

            new DashboardBreakdownItemDto
            {
                Code =
                    "REACTIVE",

                Label =
                    "Reactive",

                Count =
                    reactive
            }
        ];
    }

    private static bool IsPpmCallType(
        string? callType)
    {
        var cleanType =
            CleanCode(
                callType);

        return cleanType is
            ">" or
            "P";
    }

    private static bool IsProjectCallType(
        string? callType)
    {
        var cleanType =
            CleanCode(
                callType);

        return cleanType is
            "=" or
            "L" or
            "I" or
            "A" or
            "D" or
            "@" or
            "*" or
            "Š" or
            "‰" or
            "®";
    }

    private static bool IsReactiveCallType(
        string? callType)
    {
        return
            !IsPpmCallType(
                callType) &&
            !IsProjectCallType(
                callType);
    }

    // =========================================================
    // SYSTEM TYPE BREAKDOWN
    // =========================================================

    private static List<DashboardBreakdownItemDto>
        BuildSystemTypeBreakdown(
            List<PortalCallDto> calls,
            Dictionary<string, string> categoryMap)
    {
        return calls
            .Select(call =>
                GetCallSystemCategoryCode(
                    call,
                    categoryMap))
            .GroupBy(category =>
                category)
            .Select(group =>
                new DashboardBreakdownItemDto
                {
                    Code =
                        group.Key,

                    Label =
                        GetSystemCategoryLabel(
                            group.Key),

                    Count =
                        group.Count()
                })
            .OrderByDescending(item =>
                item.Count)
            .ToList();
    }

    private static string GetCallSystemCategoryCode(
        PortalCallDto call,
        Dictionary<string, string> categoryMap)
    {
        var code =
            CleanCode(
                call.SystemType);

        if (string.IsNullOrWhiteSpace(
            code))
        {
            return "OTHER";
        }

        return categoryMap.TryGetValue(
            code,
            out var category)
                ? category
                : "OTHER";
    }

    private static string GetSystemCategory(
        string description)
    {
        var value =
            description
                .Trim()
                .ToUpperInvariant();

        if (value.Contains(
            "CCTV"))
        {
            return "CCTV";
        }

        if (value.Contains(
            "FIRE ALARM"))
        {
            return "Fire Alarm";
        }

        if (value.Contains(
            "EXTINGUISH"))
        {
            return "Extinguishers";
        }

        if (value.Contains(
            "INTRUDER"))
        {
            return "Intruder Alarm";
        }

        if (
            value.Contains(
                "IT") &&
            value.Contains(
                "COMMUNICATION"))
        {
            return "IT and Communications";
        }

        if (
            value.Contains(
                "ACCESS CONTROL") ||
            value.Contains(
                "DOOR ACCESS"))
        {
            return "Access Control";
        }

        if (value.Contains(
            "TELEPHON"))
        {
            return "Telephony System";
        }

        if (value.Contains(
            "REFUGE"))
        {
            return "Disabled Refuge";
        }

        if (
            value.Contains(
                "PANIC") ||
            value.Contains(
                "HOLD UP"))
        {
            return "Panic Alarm";
        }

        if (value.Contains(
            "INTERCOM"))
        {
            return "Intercom";
        }

        if (
            value.Contains(
                "PUBLIC ADDRESS") ||
            value.Contains(
                "SOUND SYSTEM") ||
            value.Contains(
                "PA SYSTEM"))
        {
            return "PA / Sound System";
        }

        if (value.Contains(
            "NURSE CALL"))
        {
            return "Nurse Call";
        }

        if (
            value.Contains(
                "GATE") ||
            value.Contains(
                "BARRIER"))
        {
            return "Gates and Barriers";
        }

        return "Other";
    }

    private static string GetSystemCategoryCode(
        string category)
    {
        return category switch
        {
            "CCTV" =>
                "CCTV",

            "Fire Alarm" =>
                "FIRE",

            "Extinguishers" =>
                "EXT",

            "Intruder Alarm" =>
                "INTRUDER",

            "IT and Communications" =>
                "IT",

            "Access Control" =>
                "ACCESS",

            "Telephony System" =>
                "TELEPHONY",

            "Disabled Refuge" =>
                "REFUGE",

            "Panic Alarm" =>
                "PANIC",

            "Intercom" =>
                "INTERCOM",

            "PA / Sound System" =>
                "PA",

            "Nurse Call" =>
                "NURSE",

            "Gates and Barriers" =>
                "GATES",

            _ =>
                "OTHER"
        };
    }

    private static string GetSystemCategoryLabel(
        string categoryCode)
    {
        return categoryCode switch
        {
            "CCTV" =>
                "CCTV",

            "FIRE" =>
                "Fire Alarm",

            "EXT" =>
                "Extinguishers",

            "INTRUDER" =>
                "Intruder Alarm",

            "IT" =>
                "IT and Communications",

            "ACCESS" =>
                "Access Control",

            "TELEPHONY" =>
                "Telephony System",

            "REFUGE" =>
                "Disabled Refuge",

            "PANIC" =>
                "Panic Alarm",

            "INTERCOM" =>
                "Intercom",

            "PA" =>
                "PA / Sound System",

            "NURSE" =>
                "Nurse Call",

            "GATES" =>
                "Gates and Barriers",

            _ =>
                "Other"
        };
    }

    // =========================================================
    // SUPPORT FILTERING
    // =========================================================

    private static bool MatchesDashboardFilter(
        PortalCallDto call,
        DashboardCallsFilterType filterType,
        string filterValue,
        Dictionary<string, string>? systemCategoryMap)
    {
        return filterType switch
        {
            DashboardCallsFilterType.OPEN =>
                IsOpenStatus(
                    call.CallStatus),

            DashboardCallsFilterType.COMPLETED =>
                IsStatus(
                    call.CallStatus,
                    "C"),

            DashboardCallsFilterType.FURTHER_ACTION =>
                IsStatus(
                    call.CallStatus,
                    "F"),

            DashboardCallsFilterType.STATUS =>
                IsStatus(
                    call.CallStatus,
                    filterValue),

            DashboardCallsFilterType.CALL_TYPE =>
                MatchesCallTypeCategory(
                    call.CallType,
                    filterValue),

            DashboardCallsFilterType.SYSTEM_TYPE =>
                MatchesSystemTypeCategory(
                    call,
                    filterValue,
                    systemCategoryMap),

            _ =>
                false
        };
    }

    private static bool MatchesCallTypeCategory(
        string? callType,
        string category)
    {
        return category
            .Trim()
            .ToUpperInvariant()
            switch
        {
            "PPM" =>
                IsPpmCallType(
                    callType),

            "PROJECTS" =>
                IsProjectCallType(
                    callType),

            "REACTIVE" =>
                IsReactiveCallType(
                    callType),

            _ =>
                false
        };
    }

    private static bool MatchesSystemTypeCategory(
        PortalCallDto call,
        string category,
        Dictionary<string, string>? categoryMap)
    {
        if (categoryMap is null)
        {
            return false;
        }

        var actualCategory =
            GetCallSystemCategoryCode(
                call,
                categoryMap);

        return string.Equals(
            actualCategory,
            category,
            StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================
    // GENERAL CALL HELPERS
    // =========================================================

    private static List<PortalCallDto>
        RemoveCancelledCalls(
            IEnumerable<PortalCallDto> calls)
    {
        return calls
            .Where(call =>
                !string.Equals(
                    CleanCode(
                        call.CallType),
                    "X",
                    StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static bool IsOpenStatus(
        string? status)
    {
        return CleanCode(
            status) is
            "N" or
            "A" or
            "E" or
            "F" or
            "R";
    }

    private static bool IsStatus(
        string? status,
        string expected)
    {
        return string.Equals(
            CleanCode(
                status),
            CleanCode(
                expected),
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

    private static bool RequiresFilterValue(
        DashboardCallsFilterType filterType)
    {
        return filterType is
            DashboardCallsFilterType.STATUS or
            DashboardCallsFilterType.CALL_TYPE or
            DashboardCallsFilterType.SYSTEM_TYPE;
    }
}