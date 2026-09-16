namespace mysystem_bff.Services.Interfaces.Calendar;

using mysystem_bff.Models.Admin;
using mysystem_bff.Models.Portal.EventsCalendar;

public interface ICalendarCallsService
{
    Task<ServiceResult<List<PortalCalendarCallEventDto>>>
        GetEventsAsync(
            PortalCalendarCallsQuery query,
            CancellationToken ct = default);
}