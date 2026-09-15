namespace mysystem_bff.Models.Portal.EventsCalendar;

public class PortalCalendarQuery
{
    public string? CustomerNo { get; set; }
    public string? SiteId { get; set; }

    public int Year { get; set; }
    public int Month { get; set; }
}