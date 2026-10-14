namespace Mauw.Erp.Web.Helpers;

public static class OrderMatrixCalculator
{
    public static Dictionary<(string Size, string Color), int> NormalizeQuantities(
        IReadOnlyDictionary<(string Size, string Color), int>? quantities)
    {
        var normalized = new Dictionary<(string Size, string Color), int>();

        if (quantities is null)
        {
            return normalized;
        }

        foreach (var (key, value) in quantities)
        {
            if (value <= 0)
            {
                continue;
            }

            normalized[key] = value;
        }

        return normalized;
    }

    public static int GetTotalQuantity(IReadOnlyDictionary<(string Size, string Color), int>? quantities)
    {
        return quantities is null ? 0 : quantities.Values.Sum();
    }
}
