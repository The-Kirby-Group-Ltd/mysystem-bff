namespace mysystem_bff.Models.Portal.Responses;

using mysystem_bff.Models.Portal;

public class PortalCallsResponse
{
    public List<PortalCallDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public bool HasMore { get; set; }
}
