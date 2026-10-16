namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// Lightweight DTO for contact list tables.
/// </summary>
public record ContactSummaryDto(
    Guid Id,
    string BusinessName,
    string? VatNumber,
    string? TaxCode,
    string? City,
    string? Province,
    string Status,
    bool IsCustomer,
    bool IsSupplier,
    bool IsAgent,
    DateTime CreatedAt
);
