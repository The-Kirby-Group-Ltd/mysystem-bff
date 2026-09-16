using mysystem_bff.Models.Admin;
using mysystem_bff.Models.Middleware.Calls;
using mysystem_bff.Models.Portal.EventsCalendar;

using mysystem_bff.Services.Interfaces.Calendar;
using mysystem_bff.Services.Interfaces.GenericData;

namespace mysystem_bff.Services.Services.Calendar;

public class CalendarCallsService
    : ICalendarCallsService
{
    private readonly IMiddlewareCallActionsService
        _callActionsService;

    public CalendarCallsService(
        IMiddlewareCallActionsService callActionsService)
    {
        _callActionsService =
            callActionsService;
    }

    // =========================================================
    // Calendar calls
    // =========================================================

    public async Task<ServiceResult<List<PortalCalendarCallEventDto>>>
        GetEventsAsync(
            PortalCalendarCallsQuery query,
            CancellationToken ct = default)
    {
        var customerNo =
            query.CustomerNo?
                .Trim()
                .ToUpperInvariant()
            ?? "";

        var from =
            query.AppointmentFrom.Date;

        var to =
            query.AppointmentTo.Date;

        // =====================================================
        // Validate
        // =====================================================

        if (string.IsNullOrWhiteSpace(customerNo))
        {
            return ServiceResult<List<PortalCalendarCallEventDto>>.Fail(
                "Customer No is required.",
                400);
        }

        if (to < from)
        {
            return ServiceResult<List<PortalCalendarCallEventDto>>.Fail(
                "Appointment To cannot be before Appointment From.",
                400);
        }

        /*
         * The frontend deliberately requests one calendar
         * week at a time.
         */
        if ((to - from).TotalDays > 6)
        {
            return ServiceResult<List<PortalCalendarCallEventDto>>.Fail(
                "Calendar call requests cannot exceed seven days.",
                400);
        }

        // =====================================================
        // First attempt: entire requested week
        // =====================================================

        var weekResult =
            await _callActionsService
                .GetCalendarCallActions(
                    customerNo,
                    from,
                    to,
                    ct);

        if (!weekResult.Success)
        {
            return ServiceResult<List<PortalCalendarCallEventDto>>.Fail(
                weekResult.Error ??
                    "Unable to retrieve calendar call appointments.",
                weekResult.StatusCode);
        }

        if (weekResult.Data is null)
        {
            return ServiceResult<List<PortalCalendarCallEventDto>>.Fail(
                "Middleware returned no calendar call data.",
                502);
        }

        // =====================================================
        // Normal case
        // =====================================================

        if (!weekResult.Data.Truncated)
        {
            var events =
                MapEvents(
                    weekResult.Data.Items,
                    from,
                    to);

            return ServiceResult<List<PortalCalendarCallEventDto>>
                .Ok(events);
        }

        // =====================================================
        // Fallback: individual days
        // =====================================================

        var dailyActions =
            new List<MiddlewareCalendarCallAction>();

        for (
            var date = from;
            date <= to;
            date = date.AddDays(1))
        {
            var dayResult =
                await _callActionsService
                    .GetCalendarCallActions(
                        customerNo,
                        date,
                        date,
                        ct);

            if (!dayResult.Success)
            {
                return ServiceResult<List<PortalCalendarCallEventDto>>.Fail(
                    dayResult.Error ??
                        $"Unable to retrieve calendar calls for {date:dd/MM/yyyy}.",
                    dayResult.StatusCode);
            }

            if (dayResult.Data is null)
            {
                return ServiceResult<List<PortalCalendarCallEventDto>>.Fail(
                    $"Middleware returned no calendar call data for {date:dd/MM/yyyy}.",
                    502);
            }

            /*
             * Even one day has exceeded 100 appointments.
             *
             * MMAPI cannot subdivide the request any further,
             * so returning this dataset would silently omit
             * appointments.
             */
            if (dayResult.Data.Truncated)
            {
                return ServiceResult<List<PortalCalendarCallEventDto>>.Fail(
                    $"Calendar call appointments for {date:dd/MM/yyyy} exceeded the 100-record MMAPI limit.",
                    502);
            }

            dailyActions.AddRange(
                dayResult.Data.Items);
        }

        var dailyEvents =
            MapEvents(
                dailyActions,
                from,
                to);

        return ServiceResult<List<PortalCalendarCallEventDto>>
            .Ok(dailyEvents);
    }

    // =========================================================
    // Map / clean
    // =========================================================

    private static List<PortalCalendarCallEventDto>
        MapEvents(
            IEnumerable<MiddlewareCalendarCallAction> actions,
            DateTime from,
            DateTime to)
    {
        return actions
            .Where(action =>
                action.CallNumber > 0)
            .Where(action =>
                action.CallActionNumber > 0)
            .Where(action =>
                action.AppointmentDate.HasValue)
            .Where(action =>
                action.AppointmentDate!.Value.Date >=
                    from.Date &&
                action.AppointmentDate.Value.Date <=
                    to.Date)
            /*
             * Defensive de-duplication.
             *
             * CallNumber + CallActionNumber uniquely identifies
             * the appointment action for our purposes.
             */
            .GroupBy(action =>
                new
                {
                    action.CallNumber,
                    action.CallActionNumber
                })
            .Select(group =>
                group.First())
            .Select(action =>
                new PortalCalendarCallEventDto
                {
                    CallNumber =
                        action.CallNumber,

                    CallActionNumber =
                        action.CallActionNumber,

                    SiteName =
                        action.SiteName?
                            .Trim()
                        ?? "",

                    Engineer =
                        action.Engineer?
                            .Trim()
                            .ToUpperInvariant()
                        ?? "",

                    CallStatus =
                        action.CallStatus?
                            .Trim()
                            .ToUpperInvariant()
                        ?? "",

                    AppointmentDate =
                        action.AppointmentDate!.Value.Date,

                    AppointmentFromTime =
                        action.AppointmentFromTime?
                            .Trim(),

                    AppointmentToTime =
                        action.AppointmentToTime?
                            .Trim()
                })
            .OrderBy(calendarEvent =>
                calendarEvent.AppointmentDate)
            .ThenBy(calendarEvent =>
                calendarEvent.AppointmentFromTime)
            .ThenBy(calendarEvent =>
                calendarEvent.CallNumber)
            .ToList();
    }
}