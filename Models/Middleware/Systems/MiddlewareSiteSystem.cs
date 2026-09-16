namespace mysystem_bff.Models.Middleware.Systems;

public class MiddlewareSiteSystem
{
    public required int SystemNo { get; set; }
    public required string SiteId { get; set; }
    public required string SystemCode { get; set; }
    public required string Status { get; set; }
    public required string Maintained_YN { get; set; }
    public string? CommissionedDate { get; set; }
    public string? LastMaintenanceDate { get; set; }
    public string? NextMaintenanceDate { get; set; }
}
