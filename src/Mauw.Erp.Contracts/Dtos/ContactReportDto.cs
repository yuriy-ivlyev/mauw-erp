namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// DTO for contact information in reports.
/// </summary>
public record ContactReportDto(
    string BusinessName,
    string VatNumber,
    string? SdiCode,
    string? Address = null,
    string? City = null,
    string? PostalCode = null,
    string? Province = null,
    string? Country = null
);