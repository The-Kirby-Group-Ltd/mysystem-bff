namespace mysystem_bff.Models.Portal.EventsCalendar;

public class PortalCalendarMaintenanceEventDto
{
    public string SiteId { get; set; } = "";

    public int SystemNo { get; set; }

    public DateTime MaintenanceDate { get; set; }

    public string Description { get; set; } = "";
}