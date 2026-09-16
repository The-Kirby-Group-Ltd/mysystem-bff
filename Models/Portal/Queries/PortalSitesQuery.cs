namespace mysystem_bff.Models.Portal.Queries;

public class PortalSitesQuery
{
    public string? CustomerNo { get; set; } = "";
    public string? SiteId { get; set; }
    public string? PostCode { get; set; }
    public string? Status { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}