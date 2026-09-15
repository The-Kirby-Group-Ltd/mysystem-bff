namespace mysystem_bff.Models.Portal.EventsCalendar;

public class PortalCalendarCallEventDto
{
    public int CallNumber { get; set; }
    public int CallActionNumber { get; set; }

    public string SiteId { get; set; } = "";

    public DateTime AppointmentDate { get; set; }

    public string? AppointmentFromTime { get; set; }

    public string Engineer { get; set; } = "";
}