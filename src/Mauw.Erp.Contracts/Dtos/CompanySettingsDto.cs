namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// Dati completi della Nostra Azienda (Company Settings).
/// </summary>
public record CompanySettingsDto(
    Guid Id,
    string BusinessName,
    string? VatNumber,
    string? TaxCode,
    string? SdiCode,
    string? PecEmail,
    string CountryCode,
    string? FiscalRegime,
    string? ReaNumber,
    string? ReaOffice,
    decimal? ShareCapital,
    bool? IsSoleShareholder,
    bool IsInLiquidation,
    string? LogoUrl,
    CompanyAddressSiteDto? LegalHeadquarters,
    List<CompanyAddressSiteDto> OperationalSites,
    bool IsShareCapitalFullyPaid = false,
    string? BankName = null,
    string? Iban = null,
    string? BicSwift = null,
    string? InvoiceNotes = null
);

public record CompanyAddressSiteDto(
    Guid Id,
    string Purpose,
    string? SiteName,
    string Street,
    string ZipCode,
    string City,
    string? Province,
    string CountryCode,
    bool IsMainOperationalSite,
    string? Phone,
    string? Email,
    string? AccessNotes,
    double? Latitude,
    double? Longitude,
    string? FormattedAddress,
    Guid? LinkedWarehouseId
);
