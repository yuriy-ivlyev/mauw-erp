namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// DTO for Contact information (response).
/// </summary>
public record ContactDto(
    Guid Id,
    int Type,
    string BusinessName,
    string VatNumber,
    string? SdiCode,
    decimal DefaultDiscountRate,
    decimal DefaultCommissionRate,
    Guid? AgentId,
    DateTime CreatedAt,
    DateTime UpdatedAt
);