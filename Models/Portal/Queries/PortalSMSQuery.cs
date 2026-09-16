namespace mysystem_bff.Models.Portal.Queries
{
    public class PortalSMSQuery
    {
        public string? CustomerNo { get; set; } = "";
        public required string SiteId { get; set; }
        public required int SystemNo { get; set; }
    }
}
