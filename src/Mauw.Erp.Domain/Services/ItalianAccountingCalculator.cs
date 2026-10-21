using System.Collections.Immutable;
using System.Globalization;
using Mauw.Erp.Domain.Accounting;

namespace Mauw.Erp.Domain.Services;

/// <summary>Rounds taxable line totals, then calculates VAT once per rate/nature group.</summary>
public sealed class ItalianAccountingCalculator
{
    private static readonly ImmutableHashSet<string> NatureCodes =
        ImmutableHashSet.Create(StringComparer.Ordinal,
            "N1", "N2.1", "N2.2", "N3.1", "N3.2", "N3.3", "N3.4", "N3.5", "N3.6",
            "N4", "N5", "N6.1", "N6.2", "N6.3", "N6.4", "N6.5", "N6.6", "N6.7",
            "N6.8", "N6.9", "N7");

    /// <summary>Additional taxable expenses must be supplied as ordinary classified lines.</summary>
    public AccountingResult<CalculatedDocument> Calculate(IEnumerable<DocumentLineInput>? lines)
    {
        if (lines is null)
            return AccountingResult<CalculatedDocument>.Fail("Lines are required.");

        try
        {
            var calculatedLines = ImmutableArray.CreateBuilder<CalculatedDocumentLine>();
            foreach (var line in lines)
            {
                var result = CalculateLine(line, checked(calculatedLines.Count + 1));
                if (!result.Success)
                    return AccountingResult<CalculatedDocument>.Fail(result.Error);

                calculatedLines.Add(result.Data!);
            }

            var summaries = ImmutableArray.CreateBuilder<VatSummary>();
            foreach (var group in calculatedLines.GroupBy(line => (line.VatRate, line.NatureCode)))
            {
                var taxable = Money(group.Sum(line => line.LineNetTotal));
                var vat = group.Key.VatRate == 0m ? 0.00m : Money(taxable * group.Key.VatRate / 100m);
                summaries.Add(new VatSummary(group.Key.VatRate, group.Key.NatureCode, taxable, vat));
            }

            var totalTaxable = Money(summaries.Sum(summary => summary.TaxableAmount));
            var totalVat = Money(summaries.Sum(summary => summary.VatAmount));
            return AccountingResult<CalculatedDocument>.Ok(new CalculatedDocument(
                calculatedLines.ToImmutable(), summaries.ToImmutable(),
                totalTaxable, totalVat, Money(totalTaxable + totalVat)));
        }
        catch (OverflowException)
        {
            return AccountingResult<CalculatedDocument>.Fail("Document calculation exceeds decimal capacity.");
        }
    }

    public AccountingResult<CalculatedDocumentLine> CalculateLine(DocumentLineInput? line, int lineNumber = 1)
    {
        if (line is null)
            return AccountingResult<CalculatedDocumentLine>.Fail("Line is required.");
        if (lineNumber < 1)
            return AccountingResult<CalculatedDocumentLine>.Fail("Line number must be positive.");
        if (line.Quantity == 0m)
            return Invalid("Quantity must not be zero.");
        var priceError = ValidatePrice(line.UnitPrice, line.DiscountPercent);
        if (priceError is not null)
            return Invalid(priceError);
        if (line.VatRate is < 0m or > 100m)
            return Invalid("VAT rate must be between zero and 100.");
        if (decimal.Round(line.VatRate, 2, MidpointRounding.AwayFromZero) != line.VatRate)
            return Invalid("VAT rate must have at most two fractional decimal places.");
        if (line.VatRate == 0m && (line.NatureCode is null || !NatureCodes.Contains(line.NatureCode)))
            return Invalid("A supported detailed SDI nature code is required for zero VAT.");
        if (line.VatRate > 0m && line.NatureCode is not null)
            return Invalid("Nature code must be null for positive VAT.");

        try
        {
            var unitNetPrice = line.PriceIncludesVat
                ? RequireScale(decimal.Round(line.UnitPrice / (1m + line.VatRate / 100m),
                    8, MidpointRounding.AwayFromZero) + 0.00000000m, 8)
                : RequireScale(decimal.Parse(line.UnitPrice.ToString("0.00######", CultureInfo.InvariantCulture),
                    CultureInfo.InvariantCulture), 2);
            var netTotal = Money(line.Quantity * unitNetPrice * (1m - line.DiscountPercent / 100m));
            return AccountingResult<CalculatedDocumentLine>.Ok(new CalculatedDocumentLine(
                lineNumber, line.Quantity, unitNetPrice, line.DiscountPercent,
                line.VatRate, line.NatureCode, netTotal));
        }
        catch (OverflowException)
        {
            return Invalid("Calculation exceeds decimal capacity.");
        }

        AccountingResult<CalculatedDocumentLine> Invalid(string error) =>
            AccountingResult<CalculatedDocumentLine>.Fail($"Line {lineNumber}: {error}");
    }

    private static decimal Money(decimal value) =>
        RequireScale(decimal.Round(value, 2, MidpointRounding.AwayFromZero) + 0.00m, 2);

    public AccountingResult<DiscountedUnitPrice> CalculateDiscountedUnitPrice(
        decimal unitPrice, decimal discountPercent)
    {
        var error = ValidatePrice(unitPrice, discountPercent);
        if (error is not null)
            return AccountingResult<DiscountedUnitPrice>.Fail(error);
        try
        {
            return AccountingResult<DiscountedUnitPrice>.Ok(new DiscountedUnitPrice(
                decimal.Round(unitPrice * (1m - discountPercent / 100m), 8, MidpointRounding.AwayFromZero)));
        }
        catch (OverflowException)
        {
            return AccountingResult<DiscountedUnitPrice>.Fail("Unit price calculation exceeds decimal capacity.");
        }
    }

    private static string? ValidatePrice(decimal unitPrice, decimal discountPercent)
    {
        if (unitPrice < 0m)
            return "Unit price must not be negative.";
        if (decimal.Round(unitPrice, 8, MidpointRounding.AwayFromZero) != unitPrice)
            return "Unit price must have at most eight fractional decimal places.";
        return discountPercent is < 0m or > 100m ? "Discount must be between zero and 100." : null;
    }

    /// <summary>
    /// Combines the discounts of one line, in order, into the single percentage used by <see cref="CalculateLine"/>.
    /// Percentages compound on the amount left by the previous discounts; an amount is a discount on the line total
    /// expressed in euros (two decimals) and cannot exceed what is left. The effective percentage is rounded
    /// once, to ten decimals, so the discount is still applied a single time to the line total.
    /// Saved amounts are for presentation only and may differ from the total by rounding.
    /// </summary>
    public AccountingResult<CombinedDiscount> CombineDiscounts(
        decimal quantity, decimal unitPrice, IReadOnlyList<DiscountInput>? discounts)
    {
        if (discounts is null || discounts.Any(discount => discount is null))
            return AccountingResult<CombinedDiscount>.Fail("Discounts are required.");
        if (quantity <= 0m)
            return AccountingResult<CombinedDiscount>.Fail("Quantity must be positive to apply discounts.");
        var priceError = ValidatePrice(unitPrice, 0m);
        if (priceError is not null)
            return AccountingResult<CombinedDiscount>.Fail(priceError);

        try
        {
            var gross = quantity * unitPrice;
            var remaining = gross;
            var saved = new List<decimal>(discounts.Count);
            foreach (var discount in discounts)
            {
                if (discount.Percent.HasValue == discount.Amount.HasValue)
                    return AccountingResult<CombinedDiscount>.Fail("Each discount needs exactly one of percentage or amount.");
                decimal cut;
                if (discount.Percent is { } percent)
                {
                    if (percent is < 0m or > 100m)
                        return AccountingResult<CombinedDiscount>.Fail("Discount must be between zero and 100.");
                    cut = remaining * percent / 100m;
                }
                else
                {
                    cut = discount.Amount!.Value;
                    if (cut < 0m || decimal.Round(cut, 2, MidpointRounding.AwayFromZero) != cut)
                        return AccountingResult<CombinedDiscount>.Fail("Discount amount must not be negative and has at most two decimals.");
                    if (cut > remaining)
                        return AccountingResult<CombinedDiscount>.Fail("Discount amount exceeds the line amount.");
                }
                saved.Add(Money(cut));
                remaining -= cut;
            }

            var effective = gross == 0m
                ? 0m
                : decimal.Round((1m - remaining / gross) * 100m, 10, MidpointRounding.AwayFromZero);
            // Dividing by a value with many zeros drops insignificant trailing zeros.
            return AccountingResult<CombinedDiscount>.Ok(new CombinedDiscount(
                effective / 1.0000000000000000000000000000m, saved));
        }
        catch (OverflowException)
        {
            return AccountingResult<CombinedDiscount>.Fail("Discount calculation exceeds decimal capacity.");
        }
    }

    private static decimal RequireScale(decimal value, int minimumScale)
    {
        // Decimal can silently drop trailing zeroes near its capacity; do not return the wrong scale.
        if (((decimal.GetBits(value)[3] >> 16) & 0xff) < minimumScale)
            throw new OverflowException();
        return value;
    }
}
