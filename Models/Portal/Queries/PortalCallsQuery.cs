namespace mysystem_bff.Models.Portal.Queries;

public class PortalCallsQuery
{
	public string? CustomerNo { get; set; }
	public string? SiteId { get; set; }
	public int CallNumber { get; set; } = 0;
	public DateTime? LoggedFrom { get; set; }
	public DateTime? LoggedTo { get; set; }
	public string? Engineer { get; set; }
	public string? SystemType { get; set; }
	public int Page { get; set; } = 1;
	public int PageSize { get; set; } = 10;
}
