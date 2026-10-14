using Mauw.Erp.Web.Helpers;

namespace Mauw.Erp.UnitTests;

public class OrderMatrixCalculatorTests
{
    [Fact]
    public void NormalizeQuantities_IgnoresNonPositiveValues()
    {
        var quantities = new Dictionary<(string Size, string Color), int>
        {
            [("XS", "BCO")] = 2,
            [("S", "BCO")] = 0,
            [("M", "NRO")] = 5,
        };

        var result = OrderMatrixCalculator.NormalizeQuantities(quantities);

        Assert.Equal(2, result[("XS", "BCO")]);
        Assert.Equal(5, result[("M", "NRO")]);
        Assert.False(result.ContainsKey(("S", "BCO")));
    }

    [Fact]
    public void GetTotalQuantity_ReturnsSumOfAllQuantities()
    {
        var quantities = new Dictionary<(string Size, string Color), int>
        {
            [("XS", "BCO")] = 2,
            [("S", "BCO")] = 3,
            [("M", "NRO")] = 4,
        };

        Assert.Equal(9, OrderMatrixCalculator.GetTotalQuantity(quantities));
    }
}
