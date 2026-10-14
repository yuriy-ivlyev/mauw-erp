namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// DTO for DDT (Transport Document) information (response).
/// </summary>
public record DdtDto(
    Guid Id,
    string DdtNumber,
    Guid ContactId,
    DateTime DdtDate,
    decimal TaxableTotal,
    decimal VatTotal,
    decimal GrossTotal,
    List<DdtItemDto> Items
);