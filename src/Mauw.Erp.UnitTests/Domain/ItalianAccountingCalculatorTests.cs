using System.Collections.Immutable;
using System.Globalization;
using Mauw.Erp.Domain.Accounting;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.UnitTests.Domain;

public sealed class ItalianAccountingCalculatorTests
{
    private readonly ItalianAccountingCalculator _calculator = new();

    [Fact]
    public void StandardVatAndAdditionalExpenseAreOrdinaryLines()
    {
        var document = Calculate(new(2m, 100m, 22m), new(1m, 5m, 22m));

        Assert.Equal(new[] { 1, 2 }, document.Lines.Select(line => line.LineNumber));
        Assert.Equal(new VatSummary(22m, null, 205.00m, 45.10m), Assert.Single(document.VatSummary));
        AssertTotals(document, 205.00m, 45.10m, 250.10m);
    }

    [Fact]
    public void MixedRatesAndExemptionAreGroupedSeparately()
    {
        var document = Calculate(
            new(2m, 100m, 22m), new(1m, 30m, 10m), new(1m, 50m, 0m, "N3.2"),
            new(1m, 10m, 22m));

        Assert.Equal(new[]
        {
            new VatSummary(22m, null, 210.00m, 46.20m),
            new VatSummary(10m, null, 30.00m, 3.00m),
            new VatSummary(0m, "N3.2", 50.00m, 0.00m)
        }, document.VatSummary);
        AssertTotals(document, 290.00m, 49.20m, 339.20m);
    }

    [Fact]
    public void ZeroVatNatureCodesRemainDistinctAndAreNeverFabricated()
    {
        var document = Calculate(new(1m, 10m, 0m, "N1"), new(1m, 20m, 0m, "N3.2"),
            new(-1m, 2m, 0m, "N1"));

        Assert.Equal(new[]
        {
            new VatSummary(0m, "N1", 8.00m, 0.00m),
            new VatSummary(0m, "N3.2", 20.00m, 0.00m)
        }, document.VatSummary);
        AssertTotals(document, 28.00m, 0.00m, 28.00m);
    }

    [Theory]
    [InlineData("23", "18.85245902", "94.26", "20.74", "115.00")]
    [InlineData("94.18", "77.19672131", "385.98", "84.92", "470.90")]
    public void GrossPriceIsScorporatedToEightPlacesBeforeQuantity(
        string grossPrice, string netPrice, string taxable, string vat, string gross)
    {
        var document = Calculate(new DocumentLineInput(5m, D(grossPrice), 22m, PriceIncludesVat: true));
        var line = Assert.Single(document.Lines);

        Assert.Equal(D(netPrice), line.UnitNetPrice);
        Assert.Equal(8, Scale(line.UnitNetPrice));
        Assert.Equal(D(taxable), line.LineNetTotal);
        AssertTotals(document, D(taxable), D(vat), D(gross));
    }

    [Fact]
    public void VatIsRoundedOncePerGroupNotPerLineOrUnit()
    {
        var document = Calculate(new(1m, 0.03m, 22m), new(1m, 0.03m, 22m));

        AssertTotals(document, 0.06m, 0.01m, 0.07m);
        Assert.Equal(0.01m, Assert.Single(document.VatSummary).VatAmount);
        Assert.Equal(0.02m, document.Lines.Sum(line =>
            decimal.Round(line.LineNetTotal * 22m / 100m, 2, MidpointRounding.AwayFromZero)));
    }

    [Theory]
    [InlineData("1.5", "2.335", "0", "3.50")]
    [InlineData("-1.5", "2.335", "0", "-3.50")]
    [InlineData("3", "0.05", "10", "0.14")]
    [InlineData("-3", "0.05", "10", "-0.14")]
    [InlineData("1", "0.005", "0", "0.01")]
    [InlineData("-1", "0.005", "0", "-0.01")]
    [InlineData("2.3456789", "12.34567891", "12.345678", "25.38")]
    [InlineData("7", "12.34", "100", "0.00")]
    [InlineData("7", "0", "0", "0.00")]
    public void LineFormulaDoesNotRoundUnitDiscounts(
        string quantity, string price, string discount, string expected)
    {
        var input = new DocumentLineInput(D(quantity), D(price), 22m, DiscountPercent: D(discount));
        var line = Line(input, 7);

        Assert.Equal(7, line.LineNumber);
        Assert.Equal(input.Quantity, line.Quantity);
        Assert.Equal(input.DiscountPercent, line.DiscountPercent);
        Assert.Equal(input.VatRate, line.VatRate);
        Assert.Null(line.NatureCode);
        Assert.Equal(D(expected), line.LineNetTotal);
        Assert.Equal(decimal.Round(input.Quantity * input.UnitPrice * (1m - input.DiscountPercent / 100m),
            2, MidpointRounding.AwayFromZero), line.LineNetTotal);
        Assert.Equal(2, Scale(line.LineNetTotal));
    }

    [Theory]
    [InlineData("1", "0.05", "0.01")]
    [InlineData("-1", "-0.05", "-0.01")]
    public void GroupVatMidpointsRoundAwayFromZero(string quantity, string taxable, string vat)
    {
        var document = Calculate(new DocumentLineInput(D(quantity), 0.05m, 10m));

        AssertTotals(document, D(taxable), D(vat), D(taxable) + D(vat));
    }

    [Theory]
    [InlineData("12", "12.00", 2)]
    [InlineData("12.3", "12.30", 2)]
    [InlineData("12.34000000", "12.34", 2)]
    [InlineData("12.345", "12.345", 3)]
    [InlineData("12.34567891", "12.34567891", 8)]
    [InlineData("12.340000000", "12.34", 2)]
    [InlineData("0", "0.00", 2)]
    public void NetUnitPricesNormalizeToBetweenTwoAndEightPlaces(string price, string expected, int scale)
    {
        var line = Line(new(1m, D(price), 22m));

        Assert.Equal(D(expected), line.UnitNetPrice);
        Assert.Equal(scale, Scale(line.UnitNetPrice));
    }

    [Fact]
    public void GrossZeroVatStillPreservesEightPlaces()
    {
        var line = Line(new(1m, 23m, 0m, "N1", PriceIncludesVat: true));

        Assert.Equal(23.00000000m, line.UnitNetPrice);
        Assert.Equal(8, Scale(line.UnitNetPrice));
    }

    [Fact]
    public void GrossUnitMidpointRoundsAwayFromZeroAtEightPlaces()
    {
        var line = Line(new(1m, 0.00000003m, 100m, PriceIncludesVat: true));

        Assert.Equal(0.00000002m, line.UnitNetPrice);
        Assert.Equal(8, Scale(line.UnitNetPrice));
    }

    [Fact]
    public void GrossPriceDiscountAndFractionalQuantityUseTheEightPlaceNetFormula()
    {
        var input = new DocumentLineInput(2.34567891m, 94.18m, 22m,
            DiscountPercent: 12.345678m, PriceIncludesVat: true);
        var line = Line(input);
        var netUnit = decimal.Round(input.UnitPrice / (1m + input.VatRate / 100m),
            8, MidpointRounding.AwayFromZero);

        Assert.Equal(netUnit, line.UnitNetPrice);
        Assert.Equal(8, Scale(line.UnitNetPrice));
        Assert.Equal(decimal.Round(input.Quantity * netUnit * (1m - input.DiscountPercent / 100m),
            2, MidpointRounding.AwayFromZero), line.LineNetTotal);
        Assert.Equal(2, Scale(line.LineNetTotal));
    }

    [Fact]
    public void GroupTaxableIsExactlySumOfAlreadyRoundedLines()
    {
        var document = Calculate(new(1m, 0.005m, 22m), new(1m, 0.005m, 22m),
            new(-1m, 0.005m, 22m));

        Assert.Equal(0.01m, Assert.Single(document.VatSummary).TaxableAmount);
        Assert.Equal(document.Lines.Sum(line => line.LineNetTotal), document.TotalTaxable);
        AssertTotals(document, 0.01m, 0.00m, 0.01m);
    }

    [Fact]
    public void CreditAdjustmentCanCancelAnEntireGroup()
    {
        var document = Calculate(new(1m, 10m, 22m), new(-1m, 10m, 22m));

        Assert.Single(document.VatSummary);
        AssertTotals(document, 0.00m, 0.00m, 0.00m);
    }

    [Fact]
    public void EmptyDocumentIsSuccessfulWithScaleTwoZeros()
    {
        var document = Calculate();

        Assert.Empty(document.Lines);
        Assert.Empty(document.VatSummary);
        AssertTotals(document, 0.00m, 0.00m, 0.00m);
    }

    [Fact]
    public void NullDocumentLineAndNullElementFailExplicitly()
    {
        AssertFailure(_calculator.Calculate(null));
        AssertFailure(_calculator.CalculateLine(null));
        AssertFailure(_calculator.Calculate(new DocumentLineInput[] { null! }));
    }

    [Theory]
    [InlineData("0", "1", "22", "0", null)]
    [InlineData("1", "-1", "22", "0", null)]
    [InlineData("1", "1", "-0.01", "0", null)]
    [InlineData("1", "1", "100.01", "0", null)]
    [InlineData("1", "1", "22", "-0.01", null)]
    [InlineData("1", "1", "22", "100.01", null)]
    [InlineData("1", "1.000000001", "22", "0", null)]
    [InlineData("1", "0.000000005", "22", "0", null)]
    [InlineData("1", "1", "0", "0", null)]
    [InlineData("1", "1", "22", "0", "N1")]
    [InlineData("1", "1", "22", "0", "")]
    public void InvalidNumericAndNatureInputsReturnFailure(
        string quantity, string price, string rate, string discount, string? nature)
    {
        var input = new DocumentLineInput(D(quantity), D(price), D(rate), nature, D(discount));

        AssertFailure(_calculator.CalculateLine(input));
        AssertFailure(_calculator.Calculate([new(1m, 1m, 22m), input]));
        Assert.Contains("Line 2:", _calculator.Calculate([new(1m, 1m, 22m), input]).Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("N2")]
    [InlineData("N3")]
    [InlineData("N6")]
    [InlineData("N3.0")]
    [InlineData("N3.7")]
    [InlineData("N6.0")]
    [InlineData("N6.10")]
    [InlineData("N8")]
    [InlineData("n1")]
    [InlineData(" N1")]
    [InlineData("N1 ")]
    public void UnsupportedNatureCodesAreNotRepairedOrAccepted(string nature)
    {
        AssertFailure(_calculator.CalculateLine(new(1m, 1m, 0m, nature)));
    }

    [Theory]
    [InlineData("N1")]
    [InlineData("N2.1")]
    [InlineData("N2.2")]
    [InlineData("N3.1")]
    [InlineData("N3.2")]
    [InlineData("N3.3")]
    [InlineData("N3.4")]
    [InlineData("N3.5")]
    [InlineData("N3.6")]
    [InlineData("N4")]
    [InlineData("N5")]
    [InlineData("N6.1")]
    [InlineData("N6.2")]
    [InlineData("N6.3")]
    [InlineData("N6.4")]
    [InlineData("N6.5")]
    [InlineData("N6.6")]
    [InlineData("N6.7")]
    [InlineData("N6.8")]
    [InlineData("N6.9")]
    [InlineData("N7")]
    public void AllDetailedSdiNatureCodesAreSupported(string nature)
    {
        var document = Calculate(new DocumentLineInput(1m, 10m, 0m, nature));

        Assert.Equal(nature, Assert.Single(document.Lines).NatureCode);
        Assert.Equal(nature, Assert.Single(document.VatSummary).NatureCode);
        AssertTotals(document, 10.00m, 0.00m, 10.00m);
    }

    [Theory]
    [InlineData("0.01")]
    [InlineData("22.12")]
    [InlineData("22.120000000000000000000000000")]
    [InlineData("100.000")]
    [InlineData("100")]
    public void PositiveVatBoundaryRatesAreAccepted(string rate)
    {
        Assert.True(_calculator.CalculateLine(new(1m, 10m, D(rate))).Success);
    }

    [Theory]
    [InlineData("0.0001")]
    [InlineData("22.001")]
    [InlineData("22.125")]
    [InlineData("22.0010000")]
    [InlineData("99.999")]
    public void VatRatesBeyondTwoFractionalPlacesFailWithoutRounding(string rate)
    {
        var input = new DocumentLineInput(1m, 10m, D(rate));

        AssertFailure(_calculator.CalculateLine(input));
        AssertFailure(_calculator.Calculate([input]));
        Assert.Contains("two fractional decimal places", _calculator.CalculateLine(input).Error);
    }

    [Fact]
    public void ZeroVatWithTrailingZerosRemainsValid()
    {
        var document = Calculate(new DocumentLineInput(1m, 10m, D("0.000000"), "N1"));

        AssertTotals(document, 10.00m, 0.00m, 10.00m);
    }

    [Theory]
    [InlineData("12.34567890123456789012345678")]
    [InlineData("0.0000000000000000000000000001")]
    [InlineData("99.99999999999999999999999999")]
    public void DiscountPreservesAllDecimalPrecisionWithinZeroToOneHundred(string discount)
    {
        var input = new DocumentLineInput(1m, 100m, 22m, DiscountPercent: D(discount));
        var line = Line(input);
        var document = Calculate(input);

        Assert.Equal(decimal.GetBits(input.DiscountPercent), decimal.GetBits(line.DiscountPercent));
        Assert.Equal(decimal.GetBits(input.DiscountPercent),
            decimal.GetBits(Assert.Single(document.Lines).DiscountPercent));
        Assert.Equal(decimal.Round(input.Quantity * input.UnitPrice * (1m - input.DiscountPercent / 100m),
            2, MidpointRounding.AwayFromZero), line.LineNetTotal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidLineNumbersFail(int lineNumber)
    {
        AssertFailure(_calculator.CalculateLine(new(1m, 1m, 22m), lineNumber));
    }

    [Fact]
    public void ArithmeticAndUnrepresentableScaleOverflowReturnFailure()
    {
        AssertFailure(_calculator.CalculateLine(new(decimal.MaxValue, 2m, 22m)));
        AssertFailure(_calculator.CalculateLine(new(1m, decimal.MaxValue, 22m)));
        AssertFailure(_calculator.CalculateLine(new(1m, decimal.MaxValue, 22m, PriceIncludesVat: true)));
        AssertFailure(_calculator.Calculate([new(decimal.MaxValue, 2m, 22m)]));
    }

    [Fact]
    public void GroupSummationDocumentTotalsAndGrossOverflowReturnFailureWithoutPartialData()
    {
        var large = D("700000000000000000000000000");
        AssertFailure(_calculator.Calculate(Enumerable.Repeat(new DocumentLineInput(1m, large, 0m, "N1"), 114)));
        AssertFailure(_calculator.Calculate([new(1m, large, 100m)]));
        var halfCapacityAtScaleTwo = D("400000000000000000000000000");
        AssertFailure(_calculator.Calculate([new(1m, halfCapacityAtScaleTwo, 100m)]));
        AssertFailure(_calculator.Calculate([
            new(1m, halfCapacityAtScaleTwo, 0m, "N1"),
            new(1m, halfCapacityAtScaleTwo, 0m, "N2.1")]));
    }

    [Fact]
    public void ResultsAreRepeatableAndDoNotRetainMutableInputCollections()
    {
        var input = new List<DocumentLineInput> { new(2m, 23m, 22m, PriceIncludesVat: true) };
        var before = input[0];
        var first = _calculator.Calculate(input).Data!;
        var second = _calculator.Calculate(input).Data!;

        Assert.Equal(before, input[0]);
        Assert.Equal(first.Lines, second.Lines.ToArray());
        Assert.Equal(first.VatSummary, second.VatSummary.ToArray());
        Assert.Equal(first.TotalGross, second.TotalGross);
        input[0] = before with { UnitPrice = 999m };
        input.Clear();
        Assert.Equal(18.85245902m, Assert.Single(first.Lines).UnitNetPrice);
        Assert.IsType<ImmutableArray<CalculatedDocumentLine>>(first.Lines);
        Assert.IsType<ImmutableArray<VatSummary>>(first.VatSummary);
        Assert.Throws<NotSupportedException>(() => ((IList<CalculatedDocumentLine>)first.Lines).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<VatSummary>)first.VatSummary).Clear());
        var changedCopy = first.Lines[0] with { LineNetTotal = 999m };
        Assert.NotEqual(changedCopy.LineNetTotal, first.Lines[0].LineNetTotal);
        Assert.DoesNotContain(typeof(CalculatedDocumentLine).GetProperties(), property =>
            property.Name.Contains("VatAmount", StringComparison.Ordinal) ||
            property.Name.Contains("Gross", StringComparison.Ordinal));
    }

    [Fact]
    public void OnlyNumericOverflowIsConvertedToAResult()
    {
        Assert.Throws<InvalidOperationException>(() => _calculator.Calculate(FailingEnumeration()));

        static IEnumerable<DocumentLineInput> FailingEnumeration()
        {
            yield return new(1m, 1m, 22m);
            throw new InvalidOperationException("Enumeration failure is not an accounting input error.");
        }
    }

    private CalculatedDocument Calculate(params DocumentLineInput[] lines)
    {
        var result = _calculator.Calculate(lines);
        Assert.True(result.Success, result.Error);
        Assert.Empty(result.Error);
        return Assert.IsType<CalculatedDocument>(result.Data);
    }

    private CalculatedDocumentLine Line(DocumentLineInput input, int lineNumber = 1)
    {
        var result = _calculator.CalculateLine(input, lineNumber);
        Assert.True(result.Success, result.Error);
        Assert.Empty(result.Error);
        return Assert.IsType<CalculatedDocumentLine>(result.Data);
    }

    private static void AssertFailure<T>(AccountingResult<T> result) where T : class
    {
        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    private static void AssertTotals(CalculatedDocument document, decimal taxable, decimal vat, decimal gross)
    {
        Assert.Equal(taxable, document.TotalTaxable);
        Assert.Equal(vat, document.TotalVat);
        Assert.Equal(gross, document.TotalGross);
        Assert.Equal(2, Scale(document.TotalTaxable));
        Assert.Equal(2, Scale(document.TotalVat));
        Assert.Equal(2, Scale(document.TotalGross));
        Assert.All(document.Lines, line => Assert.Equal(2, Scale(line.LineNetTotal)));
        Assert.All(document.VatSummary, summary =>
        {
            Assert.Equal(2, Scale(summary.TaxableAmount));
            Assert.Equal(2, Scale(summary.VatAmount));
        });
    }

    private static decimal D(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

    private static int Scale(decimal value) => (decimal.GetBits(value)[3] >> 16) & 0xff;
}
