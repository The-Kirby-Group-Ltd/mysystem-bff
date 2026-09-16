namespace mysystem_bff.Services.Services.Dashboard;

using mysystem_bff.Models.Admin;
using mysystem_bff.Models.Portal.DashboardData;
using mysystem_bff.Models.Portal.Queries;
using mysystem_bff.Models.Portal.Reference;

using mysystem_bff.Services.Interfaces.Dashboard;
using mysystem_bff.Services.Interfaces.GenericData;

public class MaintenanceDashboardService
    : IMaintenanceDashboardService
{
    private readonly IMiddlewareSitesService _sitesService;
    private readonly IMiddlewareSiteSystemsService _siteSystemsService;
    private readonly IMiddlewareSmsService _smsService;
    private readonly IMiddlewareReferenceService _referenceService;

    public MaintenanceDashboardService(
        IMiddlewareSitesService sitesService,
        IMiddlewareSiteSystemsService siteSystemsService,
        IMiddlewareSmsService smsService,
        IMiddlewareReferenceService referenceService)
    {
        _sitesService = sitesService;
        _siteSystemsService = siteSystemsService;
        _smsService = smsService;
        _referenceService = referenceService;
    }

    // =========================================================
    // MAINTENANCE DASHBOARD SUMMARY
    // =========================================================

    public async Task<ServiceResult<PortalMaintenanceDashboardDataDto>>
        GetDashboardDataAsync(
            PortalDashboardDataQuery query,
            CancellationToken ct = default)
    {
        var customerNo =
            CleanCode(
                query.CustomerNo);

        var siteId =
            CleanCode(
                query.SiteId);

        if (string.IsNullOrWhiteSpace(customerNo) &&
            string.IsNullOrWhiteSpace(siteId))
        {
            return ServiceResult<PortalMaintenanceDashboardDataDto>.Fail(
                "Either Customer No or Site ID is required.",
                400);
        }

        /*
         * Maintenance status is a current-estate measurement.
         *
         * Do NOT constrain the source dataset by DataMonth/DataYear.
         * An overdue system may have a next-maintenance date in a
         * previous year, while an up-to-date system may fall in a
         * future year.
         */
        var itemsResult =
            await GetMaintenanceDashboardDataset(
                customerNo,
                siteId,
                ct);

        if (!itemsResult.Success)
        {
            return ServiceResult<PortalMaintenanceDashboardDataDto>.Fail(
                itemsResult.Error ??
                    "Unable to retrieve maintenance dashboard data.",
                itemsResult.StatusCode);
        }

        var items =
            itemsResult.Data ?? [];

        var today =
            DateTime.UtcNow.Date;

        // =====================================================
        // KPI DATA
        // =====================================================

        var upToDate =
            items.Count(item =>
                item.StatusCode == "UP_TO_DATE");

        var dueSoon =
            items.Count(item =>
                item.StatusCode == "DUE_SOON");

        var overdue =
            items.Count(item =>
                item.StatusCode == "OVERDUE");

        // =====================================================
        // STATUS BREAKDOWN
        // =====================================================

        var maintenanceStatusBreakdown =
            new List<DashboardBreakdownItemDto>
            {
                new()
                {
                    Code = "UP_TO_DATE",
                    Label = "Up to date",
                    Count = upToDate
                },

                new()
                {
                    Code = "DUE_SOON",
                    Label = "Due soon",
                    Count = dueSoon
                },

                new()
                {
                    Code = "OVERDUE",
                    Label = "Overdue",
                    Count = overdue
                }
            };

        // =====================================================
        // DUE SOON BREAKDOWN
        // =====================================================

        var dueSoonBreakdown =
            new List<DashboardBreakdownItemDto>
            {
                new()
                {
                    Code = "WITHIN_7_DAYS",
                    Label = "Within 7 days",

                    Count = items.Count(item =>
                        MatchesMaintenanceFilter(
                            item,
                            DashboardMaintenanceFilterType.WITHIN_7_DAYS,
                            today))
                },

                new()
                {
                    Code = "DAYS_8_TO_14",
                    Label = "8-14 days",

                    Count = items.Count(item =>
                        MatchesMaintenanceFilter(
                            item,
                            DashboardMaintenanceFilterType.DAYS_8_TO_14,
                            today))
                },

                new()
                {
                    Code = "DAYS_15_TO_30",
                    Label = "15-30 days",

                    Count = items.Count(item =>
                        MatchesMaintenanceFilter(
                            item,
                            DashboardMaintenanceFilterType.DAYS_15_TO_30,
                            today))
                },

                new()
                {
                    Code = "DAYS_31_TO_90",
                    Label = "31-90 days",

                    Count = items.Count(item =>
                        MatchesMaintenanceFilter(
                            item,
                            DashboardMaintenanceFilterType.DAYS_31_TO_90,
                            today))
                }
            };

        // =====================================================
        // RESULT
        // =====================================================

        var result =
            new PortalMaintenanceDashboardDataDto
            {
                CustomerNo =
                    customerNo,

                SiteId =
                    siteId,

                UpToDate =
                    upToDate,

                DueSoon =
                    dueSoon,

                Overdue =
                    overdue,

                MaintenanceStatusBreakdown =
                    maintenanceStatusBreakdown,

                DueSoonBreakdown =
                    dueSoonBreakdown
            };

        return ServiceResult<PortalMaintenanceDashboardDataDto>.Ok(
            result);
    }

    // =========================================================
    // MAINTENANCE DASHBOARD SUPPORTING RECORDS
    // =========================================================

    public async Task<ServiceResult<PortalDashboardMaintenanceItemsResponse>>
        GetDashboardItemsAsync(
            PortalDashboardMaintenanceItemsQuery query,
            CancellationToken ct = default)
    {
        var customerNo =
            CleanCode(
                query.CustomerNo);

        var siteId =
            CleanCode(
                query.SiteId);

        if (string.IsNullOrWhiteSpace(customerNo) &&
            string.IsNullOrWhiteSpace(siteId))
        {
            return ServiceResult<PortalDashboardMaintenanceItemsResponse>.Fail(
                "Either Customer No or Site ID is required.",
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

        /*
         * DataMonth and DataYear intentionally do not constrain the
         * maintenance dataset. They remain on the query model for now
         * so the current frontend/API contract does not break.
         */
        var itemsResult =
            await GetMaintenanceDashboardDataset(
                customerNo,
                siteId,
                ct);

        if (!itemsResult.Success)
        {
            return ServiceResult<PortalDashboardMaintenanceItemsResponse>.Fail(
                itemsResult.Error ??
                    "Unable to retrieve maintenance dashboard data.",
                itemsResult.StatusCode);
        }

        var today =
            DateTime.UtcNow.Date;

        var filteredItems =
            (itemsResult.Data ?? [])
                .Where(item =>
                    MatchesMaintenanceFilter(
                        item,
                        query.FilterType,
                        today))
                .OrderBy(item =>
                    item.NextMaintenanceDate)
                .ThenBy(item =>
                    item.SiteId,
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(item =>
                    item.SystemNo)
                .ToList();

        var total =
            filteredItems.Count;

        var items =
            filteredItems
                .Skip(
                    (page - 1) *
                    pageSize)
                .Take(
                    pageSize)
                .ToList();

        var result =
            new PortalDashboardMaintenanceItemsResponse
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

        return ServiceResult<PortalDashboardMaintenanceItemsResponse>.Ok(
            result);
    }

    // =========================================================
    // BUILD COMPLETE MAINTENANCE DATASET
    // =========================================================

    private async Task<ServiceResult<List<PortalMaintenanceDashboardItemDto>>>
        GetMaintenanceDashboardDataset(
            string customerNo,
            string siteId,
            CancellationToken ct)
    {
        // =====================================================
        // Resolve dashboard site scope
        // =====================================================

        var siteIdsResult =
            await GetMaintenanceDashboardSiteIds(
                customerNo,
                siteId,
                ct);

        if (!siteIdsResult.Success)
        {
            return ServiceResult<List<PortalMaintenanceDashboardItemDto>>.Fail(
                siteIdsResult.Error ??
                    "Unable to retrieve sites for maintenance dashboard.",
                siteIdsResult.StatusCode);
        }

        var siteIds =
            siteIdsResult.Data ?? [];

        if (siteIds.Count == 0)
        {
            return ServiceResult<List<PortalMaintenanceDashboardItemDto>>.Ok(
                []);
        }

        // =====================================================
        // Retrieve maintained systems
        // =====================================================

        var systemsResult =
            await _siteSystemsService
                .GetSiteSystemsForSitesAsync(
                    siteIds,
                    "Y",
                    ct);

        if (!systemsResult.Success)
        {
            return ServiceResult<List<PortalMaintenanceDashboardItemDto>>.Fail(
                systemsResult.Error ??
                    "Unable to retrieve maintained site systems.",
                systemsResult.StatusCode);
        }

        /*
         * maintained filter is present on both backend 
         * and mmapi 
         */
        var maintainedSystems =
            (systemsResult.Data ?? [])
                .Where(system =>
                    string.Equals(
                        CleanCode(system.Maintained_YN),
                        "Y",
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

        if (maintainedSystems.Count == 0)
        {
            return ServiceResult<List<PortalMaintenanceDashboardItemDto>>.Ok(
                []);
        }

        // =====================================================
        // Retrieve reliable next-maintenance schedules
        // =====================================================

        /*
         * Customer-wide:
         * pass the already-authorised CustomerNo so mmapi does not need
         * to resolve the owning customer independently for every site.
         *
         * Specific-site:
         * leave it blank so mmapi resolves the site's real customer.
         */
        var middlewareCustomerNo =
            string.IsNullOrWhiteSpace(siteId)
                ? customerNo
                : "";

        var schedulesResult =
            await _smsService
                .GetMaintenanceSchedulesForSites(
                    middlewareCustomerNo,
                    siteIds,
                    nextMaintenanceFrom: null,
                    nextMaintenanceTo: null,
                    ct);

        if (!schedulesResult.Success)
        {
            return ServiceResult<List<PortalMaintenanceDashboardItemDto>>.Fail(
                schedulesResult.Error ??
                    "Unable to retrieve maintenance schedules.",
                schedulesResult.StatusCode);
        }

        // =====================================================
        // Retrieve system type references
        // =====================================================

        var systemTypeMapResult =
            await GetSystemTypeMap(
                ct);

        if (!systemTypeMapResult.Success)
        {
            return ServiceResult<List<PortalMaintenanceDashboardItemDto>>.Fail(
                systemTypeMapResult.Error ??
                    "Unable to retrieve system type references.",
                systemTypeMapResult.StatusCode);
        }

        var systemTypeMap =
            systemTypeMapResult.Data ??
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        // =====================================================
        // Build schedule lookup
        // =====================================================

        var scheduleMap =
            (schedulesResult.Data ?? [])
                .GroupBy(
                    schedule =>
                        BuildSystemKey(
                            schedule.SiteId,
                            schedule.SystemNo),
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group.First(),
                    StringComparer.OrdinalIgnoreCase);

        // =====================================================
        // Join systems + schedules + references
        // =====================================================

        var today =
            DateTime.UtcNow.Date;

        var items =
            new List<PortalMaintenanceDashboardItemDto>();

        foreach (var system in maintainedSystems)
        {
            var systemCode =
                CleanCode(
                    system.SystemCode);

            var key =
                BuildSystemKey(
                    system.SiteId,
                    system.SystemNo);

            scheduleMap.TryGetValue(
                key,
                out var schedule);

            var lastMaintenanceDate =
                ParseNullableDate(
                    system.LastMaintenanceDate);

            var nextMaintenanceDate =
                ParseNullableDate(
                    schedule?.NextMaintenanceDate);

            var statusCode =
                GetMaintenanceStatusCode(
                    nextMaintenanceDate,
                    today);

            var systemType =
                ResolveSystemType(
                    systemCode,
                    systemTypeMap);

            items.Add(
                new PortalMaintenanceDashboardItemDto
                {
                    SiteId =
                        CleanCode(
                            system.SiteId),

                    SystemNo =
                        system.SystemNo,

                    SystemCode =
                        systemCode,

                    SystemType =
                        systemType,

                    MaintainedYN =
                        CleanCode(
                            system.Maintained_YN),

                    LastMaintenanceDate =
                        lastMaintenanceDate,

                    NextMaintenanceDate =
                        nextMaintenanceDate,

                    StatusCode =
                        statusCode,

                    StatusLabel =
                        GetMaintenanceStatusLabel(
                            statusCode)
                });
        }

        return ServiceResult<List<PortalMaintenanceDashboardItemDto>>.Ok(
            items);
    }

    // =========================================================
    // RETRIEVE CUSTOMER SITE IDS
    // =========================================================

    private async Task<ServiceResult<List<string>>>
        GetMaintenanceDashboardSiteIds(
            string customerNo,
            string siteId,
            CancellationToken ct)
    {
        /*
         * A specific-site dashboard does not need the customer's complete
         * site collection.
         */
        if (!string.IsNullOrWhiteSpace(siteId))
        {
            return ServiceResult<List<string>>.Ok(
                [siteId]);
        }

        if (string.IsNullOrWhiteSpace(customerNo))
        {
            return ServiceResult<List<string>>.Fail(
                "Customer No is required when a Site ID is not supplied.",
                400);
        }

        var siteIds =
            new List<string>();

        var page = 1;
        var hasMore = true;

        const int maximumPages = 500;

        while (
            hasMore &&
            page <= maximumPages)
        {
            var sitesResult =
                await _sitesService.GetSites(
                    new PortalSitesQuery
                    {
                        CustomerNo =
                            customerNo,

                        Page =
                            page,

                        PageSize =
                            100
                    },
                    ct);

            if (!sitesResult.Success)
            {
                return ServiceResult<List<string>>.Fail(
                    sitesResult.Error ??
                        "Unable to retrieve customer sites from middleware.",
                    sitesResult.StatusCode);
            }

            if (sitesResult.Data is null)
            {
                return ServiceResult<List<string>>.Fail(
                    "Middleware returned no site data.",
                    502);
            }

            siteIds.AddRange(
                sitesResult.Data.Items
                    .Select(site =>
                        CleanCode(
                            site.SiteId))
                    .Where(value =>
                        !string.IsNullOrWhiteSpace(value)));

            hasMore =
                sitesResult.Data.HasMore;

            page++;
        }

        if (hasMore)
        {
            return ServiceResult<List<string>>.Fail(
                "Maintenance dashboard site retrieval exceeded the maximum page limit.",
                502);
        }

        return ServiceResult<List<string>>.Ok(
            siteIds
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    // =========================================================
    // SYSTEM TYPE REFERENCES
    // =========================================================

    private async Task<ServiceResult<Dictionary<string, string>>>
        GetSystemTypeMap(
            CancellationToken ct)
    {
        var result =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        var page = 1;
        var hasMore = true;

        const int maximumPages = 100;

        while (
            hasMore &&
            page <= maximumPages)
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
                        "Unable to retrieve system type references.",
                    referenceResult.StatusCode);
            }

            if (referenceResult.Data is null)
            {
                return ServiceResult<Dictionary<string, string>>.Fail(
                    "System type reference response was empty.",
                    502);
            }

            foreach (var systemType in referenceResult.Data.Items)
            {
                var code =
                    CleanCode(
                        systemType.Code);

                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                result[code] =
                    systemType.Description?
                        .Trim()
                    ?? "";
            }

            hasMore =
                referenceResult.Data.HasMore;

            page++;
        }

        if (hasMore)
        {
            return ServiceResult<Dictionary<string, string>>.Fail(
                "System type reference retrieval exceeded the maximum page limit.",
                502);
        }

        return ServiceResult<Dictionary<string, string>>.Ok(
            result);
    }

    private static string ResolveSystemType(
        string systemCode,
        Dictionary<string, string> systemTypeMap)
    {
        if (string.IsNullOrWhiteSpace(systemCode))
        {
            return "Unknown";
        }

        if (systemTypeMap.TryGetValue(
                systemCode,
                out var description) &&
            !string.IsNullOrWhiteSpace(description))
        {
            return description;
        }

        /*
         * If reference data is missing for a particular code, keep the raw
         * code visible rather than losing useful information.
         */
        return systemCode;
    }

    // =========================================================
    // MAINTENANCE STATUS
    // =========================================================

    private static string GetMaintenanceStatusCode(
        DateTime? nextMaintenanceDate,
        DateTime today)
    {
        /* 
         * Items with no value are overdue, since 
         * overdue systems are not returned by 
         * '/future-maintenance-schedules in the api.
         */

        if (!nextMaintenanceDate.HasValue)
        {
            return "OVERDUE";
        }

        var maintenanceDate =
            nextMaintenanceDate.Value.Date;

        var todayDate =
            today.Date;

        if (maintenanceDate < todayDate)
        {
            // actually labelled as overdue
            // not likely to return anything
            return "OVERDUE";
        }

        if (maintenanceDate <=
            todayDate.AddDays(90))
        {
            return "DUE_SOON";
        }

        return "UP_TO_DATE";
    }

    private static string GetMaintenanceStatusLabel(
        string statusCode)
    {
        return statusCode switch
        {
            "UP_TO_DATE" =>
                "Up to date",

            "DUE_SOON" =>
                "Due soon",

            "OVERDUE" =>
                "Overdue",

            _ =>
                statusCode
        };
    }

    // =========================================================
    // MAINTENANCE FILTERING
    // =========================================================

    private static bool MatchesMaintenanceFilter(
        PortalMaintenanceDashboardItemDto item,
        DashboardMaintenanceFilterType filterType,
        DateTime today)
    {
        var todayDate = today.Date;

        if (!item.NextMaintenanceDate.HasValue)
        {
            return filterType == 
                DashboardMaintenanceFilterType.OVERDUE;
        }

        var maintenanceDate = 
            item.NextMaintenanceDate.Value.Date;

        return filterType switch
        {
            DashboardMaintenanceFilterType.UP_TO_DATE =>
                maintenanceDate >
                todayDate.AddDays(90),

            DashboardMaintenanceFilterType.DUE_SOON =>
                maintenanceDate >= todayDate &&
                maintenanceDate <= todayDate.AddDays(90),

            DashboardMaintenanceFilterType.OVERDUE =>
                maintenanceDate < todayDate,

            DashboardMaintenanceFilterType.WITHIN_7_DAYS =>
                maintenanceDate >= todayDate &&
                maintenanceDate <= todayDate.AddDays(7),

            DashboardMaintenanceFilterType.DAYS_8_TO_14 =>
                maintenanceDate >
                todayDate.AddDays(7) &&
                maintenanceDate <= todayDate.AddDays(14),

            DashboardMaintenanceFilterType.DAYS_15_TO_30 =>
                maintenanceDate >
                todayDate.AddDays(14) &&
                maintenanceDate <= todayDate.AddDays(30),

            DashboardMaintenanceFilterType.DAYS_31_TO_90 =>
                maintenanceDate >
                todayDate.AddDays(30) &&
                maintenanceDate <= todayDate.AddDays(90),

            _ =>
                false
        };
    }

    // =========================================================
    // GENERAL HELPERS
    // =========================================================

    private static DateTime? ParseNullableDate(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParse(
            value,
            out var parsed))
        {
            return parsed.Date;
        }

        return null;
    }

    private static string BuildSystemKey(
        string? siteId,
        int systemNo)
    {
        return
            $"{CleanCode(siteId)}|" +
            $"{systemNo}";
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