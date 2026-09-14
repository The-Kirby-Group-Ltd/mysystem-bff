using mysystem_bff.Models.Portal;

namespace mysystem_bff.Models.Portal.DashboardData;

public class PortalDashboardSlaItemsResponse
{
    public List<PortalCallDto> Items { get; set; } = [];

    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public bool HasMore { get; set; }
}