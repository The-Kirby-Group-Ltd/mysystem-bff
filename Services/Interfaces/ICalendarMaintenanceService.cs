using mysystem_bff.Models.Admin;
using mysystem_bff.Models.Portal.EventsCalendar;

namespace mysystem_bff.Services.Interfaces;

public interface ICalendarMaintenanceService
{
    Task<ServiceResult<List<PortalCalendarMaintenanceEventDto>>>
        GetEventsAsync(
            PortalCalendarQuery query,
            CancellationToken ct = default);
}