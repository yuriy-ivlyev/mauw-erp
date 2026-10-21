namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// Full contact DTO including profiles, addresses and persons.
/// </summary>
public record ContactDetailDto(
    Guid Id,
    string BusinessName,
    string? TradeName,
    string? VatNumber,
    string? TaxCode,
    string? SdiCode,
    string? PecEmail,
    string CountryCode,
    string? FiscalRegime,
    string Status,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    CustomerProfileDto? CustomerProfile,
    SupplierProfileDto? SupplierProfile,
    AgentProfileDto? AgentProfile,
    List<ContactAddressDto> Addresses,
    List<ContactPersonDto> Persons
);
