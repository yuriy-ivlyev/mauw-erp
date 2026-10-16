namespace Mauw.Erp.Contracts.Requests;

public record CreateWarehouseRequest(
    string Code,
    string Name,
    int Type,
    Guid ContactAddressId,
    bool IsSellable,
    bool IsDefault = false,
    string? Description = null);

public record UpdateWarehouseRequest(
    string Name,
    int Type,
    Guid ContactAddressId,
    bool IsSellable,
    bool IsActive,
    string? Description = null);
