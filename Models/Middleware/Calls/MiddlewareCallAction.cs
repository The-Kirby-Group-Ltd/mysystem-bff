namespace mysystem_bff.Models.Middleware.Calls
{
    public class MiddlewareCallAction
    {
        public int? CallNumber { get; set; }
        public int? CallActionNumber { get; set; }
        public string? Remarks { get; set; } 
        public string? AppointmentDate { get; set; }
        public string? AppointmentFromTime { get; set; }
        public string? StartedDate { get; set; } 
        public string? StartedTime { get; set; }
        public string? FinishedDate { get; set; } 
        public string? FinishedTime { get; set; }
        public int? HoursOnSite { get; set; } 
        public int? MinutesOnSite { get; set; }
        public string? Engineer { get; set; } 
        public string? ActionTaken { get; set; } 
        public string? SignatureName { get; set; } 
        public string? OnCallEngineersName { get; set; } 
        public string? OnRouteDate { get; set; }
        public string? OnRouteTime { get; set; }
        public string? OnSiteDate { get; set; } 
        public string? OnSiteTime { get; set; }
        public string? SLADeadlineDate { get; set; }
        public string? SLADeadlineTime { get; set; }
        public string? SLAStartDate { get; set; }
        public string? SLAStartTime { get; set; }
        public string? OvertimeType { get; set; }
        public string? OvertimeStartDate { get; set; }
        public string? OvertimeStartTime { get; set; }
        public string? OvertimeFinishDate { get; set; }
        public string? OvertimeFinishTime { get; set; }
        public string? RemoteFix_YN { get; set; } 
        public string? PropertyReferenceNo { get; set; }
        public string? Name { get; set; }
        public string? CallStatus { get; set; }
        public string? CustomerReference { get; set; }
        public string? SiteName { get; set; }
    }
}
