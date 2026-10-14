namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// DTO for stock movement (response).
/// </summary>
public record StockMovementDto(
    Guid Id,
    Guid VariantId,
    string Type,
    int Quantity,
    decimal UnitCost,
    string? ReferenceDoc,
    DateTime CreatedAt
);