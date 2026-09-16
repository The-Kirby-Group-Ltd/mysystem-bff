namespace mysystem_bff.Models.Portal.DashboardData;

using mysystem_bff.Models;

public class PortalDashboardMaintenanceItemsQuery
{
    public string CustomerNo { get; set; } = "";

    public string SiteId { get; set; } = "";

    public MonthType DataMonth { get; set; } = MonthType.ALL;

    public int DataYear { get; set; } = DateTime.UtcNow.Year;

    public DashboardMaintenanceFilterType FilterType { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 30;
}