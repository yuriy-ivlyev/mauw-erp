namespace Mauw.Erp.Contracts.Dtos;

public record WarehouseSummaryDto(
    Guid Id,
    string Code,
    string Name,
    int TypeValue,
    string TypeLabel,
    bool IsActive,
    bool IsDefault,
    bool IsSellable,
    string? SiteName,
    string City);

public record WarehouseDetailDto(
    Guid Id,
    string Code,
    string Name,
    int TypeValue,
    string TypeLabel,
    bool IsActive,
    bool IsDefault,
    bool IsSellable,
    Guid ContactAddressId,
    string? SiteName,
    string Street,
    string ZipCode,
    string City,
    string? Province,
    string CountryCode,
    string? Description,
    DateTime CreatedAt,
    DateTime UpdatedAt);
