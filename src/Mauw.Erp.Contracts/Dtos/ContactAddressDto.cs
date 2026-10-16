namespace Mauw.Erp.Contracts.Dtos;

public record ContactAddressDto(
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
