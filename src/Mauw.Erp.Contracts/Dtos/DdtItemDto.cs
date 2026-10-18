namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// DTO for DdtItem information (response).
/// </summary>
public record DdtItemDto(
    Guid Id,
    Guid DdtDocumentId,
    Guid VariantId,
    int Quantity,
    decimal UnitNetPrice,
    decimal VatRate,
    decimal LineNetTotal,
    decimal? LineVatTotal,
    decimal? LineGrossTotal,
    string? NatureCode = null,
    decimal DiscountPercent = 0m
);