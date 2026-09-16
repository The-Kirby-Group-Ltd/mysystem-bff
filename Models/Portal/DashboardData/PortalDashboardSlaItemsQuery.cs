namespace mysystem_bff.Models.Portal.DashboardData;

using mysystem_bff.Models;

public class PortalDashboardSlaItemsQuery
{
    public string? CustomerNo { get; set; }
    public string? SiteId { get; set; }

    public MonthType DataMonth { get; set; } = MonthType.ALL;

    public int DataYear { get; set; } =
        DateTime.UtcNow.Year;

    public DashboardSlaFilterType FilterType { get; set; } =
        DashboardSlaFilterType.ALL;

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 30;
}