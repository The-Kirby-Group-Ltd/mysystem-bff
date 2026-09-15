using mysystem_bff.Models.Admin;
using mysystem_bff.Models.Portal;
using mysystem_bff.Models.Portal.EventsCalendar;
using mysystem_bff.Services.Interfaces;

namespace mysystem_bff.Services.Services;

public class CalendarMaintenanceService : ICalendarMaintenanceService
{
    private readonly IMiddlewareSitesService _sitesService;
    private readonly IMiddlewareSmsService _smsService;

    public CalendarMaintenanceService(
        IMiddlewareSitesService sitesService,
        IMiddlewareSmsService smsService)
    {
        _sitesService = sitesService;
        _smsService = smsService;
    }

    // =========================================================
    // Calendar maintenance events
    // =========================================================

    public async Task<
        ServiceResult<List<PortalCalendarMaintenanceEventDto>>>
        GetEventsAsync(
            PortalCalendarQuery query,
            CancellationToken ct = default)
    {
        var validationError =
            ValidateQuery(query);

        if (validationError is not null)
        {
            return ServiceResult<
                List<PortalCalendarMaintenanceEventDto>>
                .Fail(
                    validationError,
                    400);
        }

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

        var (from, to) =
            GetMonthRange(
                query.Year,
                query.Month);

        // -----------------------------------------------------
        // Resolve sites
        // -----------------------------------------------------

        var siteIdsResult =
            await GetSiteIds(
                customerNo,
                siteId,
                ct);

        if (!siteIdsResult.Success)
        {
            return ServiceResult<
                List<PortalCalendarMaintenanceEventDto>>
                .Fail(
                    siteIdsResult.Error ??
                        "Unable to resolve calendar sites.",
                    siteIdsResult.StatusCode);
        }

        var siteIds =
            siteIdsResult.Data ?? [];

        if (siteIds.Count == 0)
        {
            return ServiceResult<
                List<PortalCalendarMaintenanceEventDto>>
                .Ok([]);
        }

        /*
         * For a customer-wide request we can pass CustomerNo
         * through to MMAPI.
         *
         * For a specific Site ID, omit it and allow MMAPI to
         * resolve the site's owning customer.
         */
        var middlewareCustomerNo =
            string.IsNullOrWhiteSpace(siteId)
                ? customerNo
                : "";

        // -----------------------------------------------------
        // Retrieve schedules
        // -----------------------------------------------------

        var schedulesResult =
            await _smsService
                .GetMaintenanceSchedulesForSites(
                    middlewareCustomerNo,
                    siteIds,
                    from,
                    to,
                    ct);

        if (!schedulesResult.Success)
        {
            return ServiceResult<
                List<PortalCalendarMaintenanceEventDto>>
                .Fail(
                    schedulesResult.Error ??
                        "Unable to retrieve maintenance calendar events.",
                    schedulesResult.StatusCode);
        }

        // -----------------------------------------------------
        // Map calendar records
        // -----------------------------------------------------

        var events =
            (schedulesResult.Data ?? [])
                .Select(schedule =>
                {
                    if (!TryParseDate(
                        schedule.NextMaintenanceDate,
                        out var maintenanceDate))
                    {
                        return null;
                    }

                    /*
                     * Defensively enforce the calendar month in
                     * the BFF even though MMAPI received the same
                     * date range.
                     */
                    if (
                        maintenanceDate.Date < from.Date ||
                        maintenanceDate.Date > to.Date)
                    {
                        return null;
                    }

                    return new PortalCalendarMaintenanceEventDto
                    {
                        SiteId =
                            schedule.SiteId?
                                .Trim()
                                .ToUpperInvariant()
                            ?? "",

                        SystemNo =
                            schedule.SystemNo,

                        MaintenanceDate =
                            maintenanceDate.Date,

                        Description =
                            schedule.Description?
                                .Trim()
                            ?? ""
                    };
                })
                .Where(calendarEvent =>
                    calendarEvent is not null)
                .Select(calendarEvent =>
                    calendarEvent!)
                .OrderBy(calendarEvent =>
                    calendarEvent.MaintenanceDate)
                .ThenBy(calendarEvent =>
                    calendarEvent.SiteId,
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(calendarEvent =>
                    calendarEvent.SystemNo)
                .ToList();

        return ServiceResult<
            List<PortalCalendarMaintenanceEventDto>>
            .Ok(events);
    }

    // =========================================================
    // Resolve calendar sites
    // =========================================================

    private async Task<ServiceResult<List<string>>>
        GetSiteIds(
            string customerNo,
            string siteId,
            CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(siteId))
        {
            return ServiceResult<List<string>>
                .Ok([siteId]);
        }

        if (string.IsNullOrWhiteSpace(customerNo))
        {
            return ServiceResult<List<string>>
                .Fail(
                    "Customer No is required when Site ID is not supplied.",
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
            var result =
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

            if (!result.Success)
            {
                return ServiceResult<List<string>>
                    .Fail(
                        result.Error ??
                            "Unable to retrieve customer sites.",
                        result.StatusCode);
            }

            if (result.Data is null)
            {
                return ServiceResult<List<string>>
                    .Fail(
                        "Middleware returned no site data.",
                        502);
            }

            siteIds.AddRange(
                result.Data.Items
                    .Select(site =>
                        site.SiteId?
                            .Trim()
                            .ToUpperInvariant()
                        ?? "")
                    .Where(value =>
                        !string.IsNullOrWhiteSpace(value)));

            hasMore =
                result.Data.HasMore;

            page++;
        }

        if (hasMore)
        {
            return ServiceResult<List<string>>
                .Fail(
                    "Calendar site retrieval exceeded the maximum page limit.",
                    502);
        }

        return ServiceResult<List<string>>
            .Ok(
                siteIds
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList());
    }

    // =========================================================
    // Validation
    // =========================================================

    private static string? ValidateQuery(
        PortalCalendarQuery query)
    {
        if (
            string.IsNullOrWhiteSpace(
                query.CustomerNo) &&
            string.IsNullOrWhiteSpace(
                query.SiteId))
        {
            return
                "Either Customer No or Site ID is required.";
        }

        if (
            query.Year < 2000 ||
            query.Year >
                DateTime.UtcNow.Year + 5)
        {
            return
                "A valid calendar year is required.";
        }

        if (
            query.Month < 1 ||
            query.Month > 12)
        {
            return
                "Calendar month must be between 1 and 12.";
        }

        return null;
    }

    // =========================================================
    // Month range
    // =========================================================

    private static (
        DateTime From,
        DateTime To)
        GetMonthRange(
            int year,
            int month)
    {
        var from =
            new DateTime(
                year,
                month,
                1);

        var to =
            from
                .AddMonths(1)
                .AddTicks(-1);

        return (
            from,
            to);
    }

    // =========================================================
    // Date helper
    // =========================================================

    private static bool TryParseDate(
        string? value,
        out DateTime date)
    {
        date = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return DateTime.TryParse(
            value,
            out date);
    }
}