namespace mysystem_bff.Models.Middleware.Sites;

public class MiddlewareSitesResponse
{
    public List<MiddlewareSite> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public bool HasMore { get; set; }
}