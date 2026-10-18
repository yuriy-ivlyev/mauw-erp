using Mauw.Erp.Domain.Accounting;
using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Domain.Services;

public static class CommercialDocumentAccountingExtensions
{
    public static AccountingResult<CalculatedDocument> CalculateAndApply(
        this ItalianAccountingCalculator calculator, IEnumerable<ICommercialDocumentLine> items)
    {
        var lines = items.ToArray();
        if (lines.Any(line => line is null))
            return AccountingResult<CalculatedDocument>.Fail("Una riga del documento non puo essere null.");
        var result = calculator.Calculate(lines.Select(line => new DocumentLineInput(
            line.Quantity, line.UnitNetPrice, line.VatRate, line.NatureCode, line.DiscountPercent)));
        if (!result.Success)
            return result;

        for (var i = 0; i < lines.Length; i++)
        {
            var calculated = result.Data!.Lines[i];
            lines[i].UnitNetPrice = calculated.UnitNetPrice;
            lines[i].DiscountPercent = calculated.DiscountPercent;
            lines[i].NatureCode = calculated.NatureCode;
            lines[i].LineNetTotal = calculated.LineNetTotal;
            // Retained only for compatibility with stored documents and older API contracts.
            lines[i].LineVatTotal = null;
            lines[i].LineGrossTotal = null;
        }

        return result;
    }
}
