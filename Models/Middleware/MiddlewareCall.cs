namespace mysystem_bff.Models.Middleware;

public class MiddlewareCall
{
    public int CallNumber { get; set; }
    public string? CallType { get; set; }
    public string? CallStatus { get; set; }
    public required string? SiteID { get; set; } = null!;
    public DateTime? LoggedDate { get; set; }
    public string? LoggingOperator { get; set; }
    public string? Engineer { get; set; }
    public string? SystemType { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? CustomerReference { get; set; }
    public string? InvoiceNo { get; set; }
    public string? LoggedRemarks { get; set; }
    public string? CompletedRemarks { get; set; }
    public DateTime? PreviousMaintenanceDate { get; set; }
    public DateTime? NextMaintenanceDate { get; set; }
    public string? FailedToRespond_YN { get; set; }
    public string? FailedToRespondReason { get; set; }
}