namespace mysystem_bff.Models.Middleware.Calls;

public class MiddlewareCallsResponse
{
    public List<MiddlewareCall> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public bool HasMore { get; set; }
}