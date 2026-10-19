using Mauw.Erp.Contracts.Pricing;
using Mauw.Erp.Domain.Accounting;
using Mauw.Erp.Domain.Pricing;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.Application.Services;

public sealed class DocumentPricingService(
    IPriceListRepository repository, IPriceListService priceLists,
    ItalianAccountingCalculator calculator) : IDocumentPricingService
{
    public async Task<AccountingResult<PreparedDocumentPricing>> PrepareAsync(
        Guid contactId, IReadOnlyList<DocumentPricingRequest> lines, DateTimeOffset at, CancellationToken ct = default)
    {
        if (lines.Any(line => line is null))
            return Fail("Le righe non possono essere null.");
        var variantIds = lines.Select(line => line.VariantId).Distinct().ToArray();
        var variants = variantIds.Length == 0 ? [] :
            (await repository.GetVariantsAsync(variantIds, ct)).ToDictionary(variant => variant.Id);
        if (variants.Count != variantIds.Length)
            return Fail("Una variante del documento non esiste.");
        var customer = await repository.GetCustomerAsync(contactId, ct);
        var sourceInvoiceIds = lines.Where(line => line.IsCustomerReturn && line.SourceLineId.HasValue)
            .Select(line => line.SourceLineId!.Value).Distinct().ToArray();
        var sourceInvoices = sourceInvoiceIds.Length == 0 ? [] :
            (await repository.GetSourceInvoiceLinesAsync(sourceInvoiceIds, contactId, ct)).ToDictionary(line => line.Id);
        var sourceOrderIds = lines.Where(line => line.SourceOrderItemId.HasValue)
            .Select(line => line.SourceOrderItemId!.Value).Distinct().ToArray();
        var sourceOrders = sourceOrderIds.Length == 0 ? [] :
            (await repository.GetSourceOrderLinesAsync(sourceOrderIds, contactId, ct)).ToDictionary(line => line.Id);
        var quotes = new Dictionary<int, ResolvedPriceLineResponse>();
        var indexed = lines.Select((line, index) => (Line: line, Index: index)).ToList();
        foreach (var group in indexed.Where(item => !item.Line.IsCustomerReturn && !item.Line.SourceOrderItemId.HasValue)
                     .GroupBy(item => item.Line.PriceListId ?? customer?.PriceListId))
        {
            var toResolve = group.Where(item => group.Key.HasValue ||
                (!item.Line.UnitNetPrice.HasValue && !item.Line.UnitGrossPrice.HasValue)).ToList();
            if (toResolve.Count == 0)
                continue;
            var resolved = await priceLists.ResolveAsync(new ResolvePricesRequest
            {
                PriceListId = group.Key, At = at,
                Lines = toResolve.Select(item => new ResolvePriceLineRequest(item.Line.VariantId, item.Line.Quantity)).ToList()
            }, ct);
            if (!resolved.Success)
                return Fail(resolved.Error!);
            for (var i = 0; i < toResolve.Count; i++)
                quotes.Add(toResolve[i].Index, resolved.Data!.Lines[i]);
        }

        var fiscalInputs = new List<DocumentLineInput>(lines.Count);
        var snapshots = new List<PricingSnapshot>(lines.Count);
        foreach (var (line, index) in indexed)
        {
            if (line.Quantity == 0m || (!line.IsCustomerReturn && line.Quantity < 0m))
                return Fail("La quantita deve essere positiva, oppure diversa da zero per i resi.");
            if (line.UnitNetPrice.HasValue && line.UnitGrossPrice.HasValue)
                return Fail("Specificare un prezzo netto oppure lordo, non entrambi.");
            if (line.OverrideReason?.Length > 500)
                return Fail("Il motivo dell'override non puo superare 500 caratteri.");
            if (!line.IsCustomerReturn && line.SourceLineId.HasValue)
                return Fail("Il documento di origine e ammesso solo per un reso cliente.");

            if (line.SourceOrderItemId.HasValue)
            {
                if (line.UnitNetPrice.HasValue || line.UnitGrossPrice.HasValue || line.SourceLineId.HasValue ||
                    line.DiscountPercent.HasValue ||
                    line.PriceListId.HasValue || line.OverrideReason is not null)
                    return Fail("L'emissione da ordine eredita i valori fiscali senza modifiche manuali.");
                if (!sourceOrders.TryGetValue(line.SourceOrderItemId.Value, out var source) ||
                    source.VariantId != line.VariantId)
                    return Fail("Riga ordine di origine non trovata per il cliente e la variante.");
                if (source.PricingSnapshot is null)
                    return Fail("La riga ordine storica non dispone di uno snapshot prezzo: richiede revisione esplicita.");
                if (line.IsCustomerReturn && !source.PricingSnapshot.SourceLineId.HasValue)
                    return Fail("Il reso cliente deve riferirsi a una riga della fattura di origine.");
                if ((line.VatRate.HasValue && line.VatRate != source.VatRate) ||
                    (line.NatureCode is not null && line.NatureCode != source.NatureCode))
                    return Fail("La classificazione fiscale deve coincidere con la riga ordine di origine.");
                if ((source.PricingSnapshot.IsCustomerReturn != line.IsCustomerReturn) ||
                    Math.Sign(source.Quantity) != Math.Sign(line.Quantity) || Math.Abs(line.Quantity) > Math.Abs((decimal)source.Quantity))
                    return Fail("Causale o quantita incompatibile con la riga ordine di origine.");
                fiscalInputs.Add(new DocumentLineInput(line.Quantity, source.UnitNetPrice,
                    source.VatRate, source.NatureCode, source.DiscountPercent));
                snapshots.Add(source.PricingSnapshot with { });
                continue;
            }

            if (line.IsCustomerReturn && line.SourceLineId.HasValue)
            {
                if (line.UnitNetPrice.HasValue || line.UnitGrossPrice.HasValue)
                    return Fail("Il prezzo del reso proviene dalla riga fattura e non puo essere modificato.");
                if (line.PriceListId.HasValue || line.OverrideReason is not null)
                    return Fail("Il reso da fattura non ammette listini o override del prezzo.");
                if (!sourceInvoices.TryGetValue(line.SourceLineId.Value, out var source) ||
                    source.VariantId != line.VariantId)
                    return Fail("Riga fattura di origine non trovata per il cliente e la variante.");
                if ((line.VatRate.HasValue && line.VatRate != source.VatRate) ||
                    (line.NatureCode is not null && line.NatureCode != source.NatureCode) ||
                    (line.DiscountPercent.HasValue && line.DiscountPercent != source.DiscountPercent))
                    return Fail("Il reso deve conservare aliquota, Natura e sconto della riga fattura.");
                fiscalInputs.Add(new DocumentLineInput(line.Quantity, source.UnitNetPrice,
                    source.VatRate, source.NatureCode, source.DiscountPercent));
                snapshots.Add(new PricingSnapshot(source.PricingSnapshot?.PriceListId,
                    source.PricingSnapshot?.RuleId, source.PricingSnapshot?.ListUnitPrice ?? source.UnitNetPrice,
                    source.PricingSnapshot?.AgreedUnitNetPrice, false, null, true, source.Id));
                continue;
            }

            if (line.IsCustomerReturn)
                return Fail("Il reso cliente richiede la riga della fattura di origine.");

            quotes.TryGetValue(index, out var quote);
            var legacyAuto = line.PreserveLegacyPrice && quote?.PriceListId is not null;
            var hasManualPrice = !legacyAuto && (line.UnitNetPrice.HasValue || line.UnitGrossPrice.HasValue);
            var manual = hasManualPrice || (quote is not null && !legacyAuto &&
                line.DiscountPercent.HasValue && line.DiscountPercent != quote.DiscountPercent);
            if (manual && quote?.PriceListId is not null && string.IsNullOrWhiteSpace(line.OverrideReason))
                return Fail("Specificare il motivo della modifica manuale del prezzo o dello sconto.");
            var unitPrice = hasManualPrice ? line.UnitGrossPrice ?? line.UnitNetPrice!.Value : quote?.ListUnitPrice;
            if (!unitPrice.HasValue)
                return Fail("Prezzo della riga non disponibile.");
            var discount = legacyAuto ? quote!.DiscountPercent :
                line.DiscountPercent ?? (quote?.PriceListId.HasValue == true ? quote.DiscountPercent : line.DefaultDiscountPercent);
            var vatRate = line.VatRate ?? quote?.VatRate;
            if (!vatRate.HasValue)
                return Fail("Specificare l'aliquota IVA della riga.");
            var fiscal = calculator.CalculateLine(new DocumentLineInput(line.Quantity, unitPrice.Value,
                vatRate.Value, line.NatureCode, discount,
                PriceIncludesVat: hasManualPrice && line.UnitGrossPrice.HasValue), index + 1);
            if (!fiscal.Success)
                return Fail(fiscal.Error);
            var calculated = fiscal.Data!;
            if (calculated.UnitNetPrice > 99999999999999.99999999m)
                return Fail("Il prezzo unitario supera la precisione supportata dalla persistenza.");
            var agreedPrice = calculator.CalculateDiscountedUnitPrice(calculated.UnitNetPrice, calculated.DiscountPercent);
            if (!agreedPrice.Success)
                return Fail(agreedPrice.Error);
            fiscalInputs.Add(new DocumentLineInput(line.Quantity, calculated.UnitNetPrice,
                calculated.VatRate, calculated.NatureCode, calculated.DiscountPercent));
            snapshots.Add(new PricingSnapshot(quote?.PriceListId, quote?.RuleId,
                quote?.ListUnitPrice ?? calculated.UnitNetPrice, agreedPrice.Data!.Value,
                manual, line.OverrideReason?.Trim(), line.IsCustomerReturn, null));
        }
        var document = calculator.Calculate(fiscalInputs);
        if (!document.Success)
            return Fail(document.Error);
        if (document.Data!.Lines.Any(line => Math.Abs(line.LineNetTotal) > 9999999999999999.99m) ||
            new[] { document.Data.TotalTaxable, document.Data.TotalVat, document.Data.TotalGross }
                .Any(total => Math.Abs(total) > 9999999999999999.99m))
            return Fail("Gli importi superano la precisione supportata dalla persistenza.");
        return AccountingResult<PreparedDocumentPricing>.Ok(new PreparedDocumentPricing(
            document.Data!.Lines.Select((line, index) => new PreparedPricingLine(line, snapshots[index])).ToList(),
            document.Data));
    }

    private static AccountingResult<PreparedDocumentPricing> Fail(string error) =>
        AccountingResult<PreparedDocumentPricing>.Fail(error);
}
