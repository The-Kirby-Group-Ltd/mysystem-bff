namespace mysystem_bff.Services.Interfaces;

using mysystem_bff.Models.Portal.EventsCalendar;
using mysystem_bff.Models.Admin;

public interface ICalendarCallsService
{
    Task<ServiceResult<List<PortalCalendarCallEventDto>>>
        GetEventsAsync(
            PortalCalendarQuery query,
            CancellationToken ct = default);
}