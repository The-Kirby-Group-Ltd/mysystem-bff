namespace mysystem_bff.Models.Portal.DashboardData;

using mysystem_bff.Models;

public class PortalDashboardDataQuery
{
    public DashboardBoardType Board { get; set; }
    public MonthType DataMonth { get; set; } = MonthType.ALL;
    public int DataYear { get; set; } = DateTime.UtcNow.Year;
    public string CustomerNo { get; set; } = "";
    public string SiteId { get; set; } = "";
}