namespace mysystem_bff.Models.Middleware.Calls;

public class MiddlewareCalendarCallAction
{
    public int CallNumber { get; set; }
    public int CallActionNumber { get; set; }

    public string? SiteName { get; set; }
    public string? Engineer { get; set; }
    public string? CallStatus { get; set; }

    public DateTime? AppointmentDate { get; set; }

    public string? AppointmentFromTime { get; set; }
    public string? AppointmentToTime { get; set; }
}
