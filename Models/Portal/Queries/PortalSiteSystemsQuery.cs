namespace mysystem_bff.Models.Portal.Queries
{
    public class PortalSiteSystemsQuery
    {
        public int? SystemNo { get; set; }
        public string SiteId { get; set; } = "";
        public string? SystemCode { get; set; }
        public string? Status { get; set; }
        public string? Maintained_YN { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }
}
