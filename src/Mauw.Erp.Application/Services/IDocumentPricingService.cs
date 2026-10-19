using Mauw.Erp.Domain.Accounting;
using Mauw.Erp.Domain.Pricing;

namespace Mauw.Erp.Application.Services;

public sealed record DocumentPricingRequest(
    Guid VariantId, decimal Quantity, decimal? UnitNetPrice, decimal? UnitGrossPrice,
    decimal? VatRate, string? NatureCode, decimal? DiscountPercent,
    Guid? PriceListId = null, string? OverrideReason = null,
    bool IsCustomerReturn = false, Guid? SourceLineId = null,
    bool PreserveLegacyPrice = false, decimal DefaultDiscountPercent = 0m, Guid? SourceOrderItemId = null);

public sealed record PreparedPricingLine(CalculatedDocumentLine FiscalLine, PricingSnapshot Snapshot);
public sealed record PreparedDocumentPricing(IReadOnlyList<PreparedPricingLine> Lines, CalculatedDocument FiscalDocument);

public interface IDocumentPricingService
{
    Task<AccountingResult<PreparedDocumentPricing>> PrepareAsync(
        Guid contactId, IReadOnlyList<DocumentPricingRequest> lines, DateTimeOffset at, CancellationToken ct = default);
}
