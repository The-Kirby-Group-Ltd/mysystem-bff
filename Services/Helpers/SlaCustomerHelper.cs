namespace mysystem_bff.Services.Helpers;

public static class SlaCustomerHelper
{
    private static readonly HashSet<string> SlaCustomerNos =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "SEL000",
            "SEL002",
            "GAT005",
            "GAT006",
            "GAT008",
            "GAT010",
            "FER002"
        };

    public static bool HasSla(
        string? customerNo)
    {
        if (string.IsNullOrWhiteSpace(customerNo))
            return false;

        return SlaCustomerNos.Contains(
            customerNo.Trim());
    }
}