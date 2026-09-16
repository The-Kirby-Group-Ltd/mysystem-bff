namespace mysystem_bff.Models.Portal.Reference;

public class PortalReferenceQuery
{
    public string? Code { get; set; }
    public string? Description { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 100;
}