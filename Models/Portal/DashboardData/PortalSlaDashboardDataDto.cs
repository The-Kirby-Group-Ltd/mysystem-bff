namespace mysystem_bff.Models.Portal.DashboardData;

public class PortalSlaDashboardDataDto
{
    public string CustomerNo { get; set; } = "";
    public string SiteId { get; set; } = "";

    public int TotalCalls { get; set; }
    public int SuccessfulCalls { get; set; }
    public int FailedCalls { get; set; }

    public decimal SuccessPercentage { get; set; }
    public decimal FailurePercentage { get; set; }

    public List<DashboardBreakdownItemDto> SlaBreakdown { get; set; } = [];
}