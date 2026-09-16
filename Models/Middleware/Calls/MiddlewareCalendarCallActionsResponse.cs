namespace mysystem_bff.Models.Middleware.Calls;

public class MiddlewareCalendarCallActionsResponse
{
    public List<MiddlewareCalendarCallAction> Items { get; set; } = [];
    public int Count { get; set; }
    public int Limit { get; set; }
    public bool Truncated { get; set; }
}
