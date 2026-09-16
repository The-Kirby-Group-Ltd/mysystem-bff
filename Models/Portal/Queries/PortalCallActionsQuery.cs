namespace mysystem_bff.Models.Portal.Queries
{
    public class PortalCallActionsQuery
    {
        public required int CallNumber { get; set; } = 0;
        public int ActionNo { get; set; } = 0;
        public string? Engineer { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }
}
