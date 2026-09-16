namespace mysystem_bff.Models.Middleware.Systems
{
    public class MiddlewareSMSResponse
    {
        public List<MiddlewareSMS> Items { get; set; } = [];
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public bool HasMore { get; set; }
    }
}
