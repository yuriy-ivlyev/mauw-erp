using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Exceptions;
using Mauw.Erp.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Mauw.Erp.Application.Services;

public class WarehouseService(
    IUnitOfWork unitOfWork,
    ILogger<WarehouseService> logger) : IWarehouseService
{
    public async Task<OperationResult<PagedResult<WarehouseSummaryDto>>> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        int? type,
        bool? isActive,
        string? sortBy = null,
        bool sortDescending = false)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 20;
        pageSize = Math.Min(pageSize, 100);

        WarehouseType? warehouseType = null;
        if (type.HasValue)
        {
            if (!Enum.IsDefined(typeof(WarehouseType), type.Value))
                return OperationResult<PagedResult<WarehouseSummaryDto>>.Fail("Invalid warehouse type");

            warehouseType = (WarehouseType)type.Value;
        }

        var (items, total) = await unitOfWork.Warehouses.GetPagedAsync(page, pageSize, search, warehouseType, isActive, sortBy, sortDescending);
        var dtos = items.Select(MapSummary).ToList();

        return OperationResult<PagedResult<WarehouseSummaryDto>>.Ok(
            new PagedResult<WarehouseSummaryDto>(dtos, page, pageSize, total));
    }

    public async Task<OperationResult<WarehouseDetailDto>> GetByIdAsync(Guid id)
    {
        var warehouse = await unitOfWork.Warehouses.GetByIdWithAddressAsync(id);
        if (warehouse is null)
            return OperationResult<WarehouseDetailDto>.Fail("Warehouse not found");

        return OperationResult<WarehouseDetailDto>.Ok(MapDetail(warehouse));
    }

    public async Task<OperationResult<WarehouseDetailDto>> CreateAsync(CreateWarehouseRequest request)
    {
        var validationError = await ValidateCreateRequestAsync(request);
        if (validationError is not null)
            return OperationResult<WarehouseDetailDto>.Fail(validationError);

        var warehouseType = (WarehouseType)request.Type;
        var normalizedCode = NormalizeCode(request.Code);

        if (await unitOfWork.Warehouses.CodeExistsAsync(normalizedCode))
            return OperationResult<WarehouseDetailDto>.Fail("Warehouse code already exists");

        var warehouse = new Warehouse(
            normalizedCode,
            request.Name,
            warehouseType,
            request.ContactAddressId,
            request.IsSellable,
            request.IsDefault,
            request.Description);

        await unitOfWork.Warehouses.AddAsync(warehouse);

        if (request.IsDefault)
        {
            var currentDefault = await unitOfWork.Warehouses.GetDefaultWarehouseAsync();
            if (currentDefault is not null)
                currentDefault.UnsetDefault();
        }

        try
        {
            await unitOfWork.SaveChangesAsync();
            logger.LogInformation("Warehouse {Id} created with code {Code}", warehouse.Id, warehouse.Code);
        }
        catch (ConcurrencyException ex)
        {
            return OperationResult<WarehouseDetailDto>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to create warehouse with code {Code}", normalizedCode);
            return OperationResult<WarehouseDetailDto>.Fail("Unable to create warehouse");
        }

        var created = await unitOfWork.Warehouses.GetByIdWithAddressAsync(warehouse.Id);
        return OperationResult<WarehouseDetailDto>.Ok(MapDetail(created!));
    }

    public async Task<OperationResult<WarehouseDetailDto>> UpdateAsync(Guid id, UpdateWarehouseRequest request)
    {
        var validationError = await ValidateUpdateRequestAsync(request);
        if (validationError is not null)
            return OperationResult<WarehouseDetailDto>.Fail(validationError);

        var warehouse = await unitOfWork.Warehouses.GetByIdWithAddressAsync(id);
        if (warehouse is null)
            return OperationResult<WarehouseDetailDto>.Fail("Warehouse not found");

        warehouse.Update(
            request.Name,
            (WarehouseType)request.Type,
            request.ContactAddressId,
            request.IsSellable,
            request.Description);

        if (request.IsActive)
            warehouse.Activate();
        else
            warehouse.Deactivate();

        await unitOfWork.Warehouses.UpdateAsync(warehouse);

        try
        {
            await unitOfWork.SaveChangesAsync();
            logger.LogInformation("Warehouse {Id} updated", warehouse.Id);
        }
        catch (ConcurrencyException ex)
        {
            return OperationResult<WarehouseDetailDto>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update warehouse {Id}", warehouse.Id);
            return OperationResult<WarehouseDetailDto>.Fail("Unable to update warehouse");
        }

        var updated = await unitOfWork.Warehouses.GetByIdWithAddressAsync(id);
        return OperationResult<WarehouseDetailDto>.Ok(MapDetail(updated!));
    }

    public async Task<OperationResult<bool>> DeleteAsync(Guid id)
    {
        var warehouse = await unitOfWork.Warehouses.GetByIdAsync(id);
        if (warehouse is null)
            return OperationResult<bool>.Fail("Warehouse not found");

        if (warehouse.IsDefault)
            return OperationResult<bool>.Fail("Default warehouse cannot be deleted");

        warehouse.SoftDelete("system");
        await unitOfWork.Warehouses.UpdateAsync(warehouse);

        try
        {
            await unitOfWork.SaveChangesAsync();
            logger.LogInformation("Warehouse {Id} soft-deleted", id);
        }
        catch (ConcurrencyException ex)
        {
            return OperationResult<bool>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete warehouse {Id}", id);
            return OperationResult<bool>.Fail("Unable to delete warehouse");
        }

        return OperationResult<bool>.Ok(true);
    }

    public async Task<OperationResult<WarehouseDetailDto>> SetDefaultAsync(Guid id)
    {
        var warehouse = await unitOfWork.Warehouses.GetByIdWithAddressAsync(id);
        if (warehouse is null)
            return OperationResult<WarehouseDetailDto>.Fail("Warehouse not found");

        var currentDefault = await unitOfWork.Warehouses.GetDefaultWarehouseAsync();
        if (currentDefault is not null && currentDefault.Id != warehouse.Id)
            currentDefault.UnsetDefault();

        warehouse.SetAsDefault();
        await unitOfWork.Warehouses.UpdateAsync(warehouse);

        try
        {
            await unitOfWork.SaveChangesAsync();
            logger.LogInformation("Warehouse {Id} set as default", id);
        }
        catch (ConcurrencyException ex)
        {
            return OperationResult<WarehouseDetailDto>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to set warehouse {Id} as default", id);
            return OperationResult<WarehouseDetailDto>.Fail("Unable to set default warehouse");
        }

        var updated = await unitOfWork.Warehouses.GetByIdWithAddressAsync(id);
        return OperationResult<WarehouseDetailDto>.Ok(MapDetail(updated!));
    }

    private async Task<string?> ValidateCreateRequestAsync(CreateWarehouseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return "Code is required";

        if (request.Code.Trim().Length > 20)
            return "Code cannot exceed 20 characters";

        if (string.IsNullOrWhiteSpace(request.Name))
            return "Name is required";

        if (request.Name.Trim().Length > 200)
            return "Name cannot exceed 200 characters";

        if (request.Description?.Trim().Length > 500)
            return "Description cannot exceed 500 characters";

        if (!Enum.IsDefined(typeof(WarehouseType), request.Type))
            return "Invalid warehouse type";

        if (!await ContactAddressExistsAsync(request.ContactAddressId))
            return "Contact address not found";

        return null;
    }

    private async Task<string?> ValidateUpdateRequestAsync(UpdateWarehouseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return "Name is required";

        if (request.Name.Trim().Length > 200)
            return "Name cannot exceed 200 characters";

        if (request.Description?.Trim().Length > 500)
            return "Description cannot exceed 500 characters";

        if (!Enum.IsDefined(typeof(WarehouseType), request.Type))
            return "Invalid warehouse type";

        if (!await ContactAddressExistsAsync(request.ContactAddressId))
            return "Contact address not found";

        return null;
    }

    private Task<bool> ContactAddressExistsAsync(Guid contactAddressId) =>
        unitOfWork.Contacts.AddressExistsAsync(contactAddressId);

    private static WarehouseSummaryDto MapSummary(Warehouse warehouse) => new(
        warehouse.Id,
        warehouse.Code,
        warehouse.Name,
        (int)warehouse.Type,
        GetTypeLabel(warehouse.Type),
        warehouse.IsActive,
        warehouse.IsDefault,
        warehouse.IsSellable,
        warehouse.Address.SiteName,
        warehouse.Address.City);

    private static WarehouseDetailDto MapDetail(Warehouse warehouse) => new(
        warehouse.Id,
        warehouse.Code,
        warehouse.Name,
        (int)warehouse.Type,
        GetTypeLabel(warehouse.Type),
        warehouse.IsActive,
        warehouse.IsDefault,
        warehouse.IsSellable,
        warehouse.ContactAddressId,
        warehouse.Address.SiteName,
        warehouse.Address.Street,
        warehouse.Address.ZipCode,
        warehouse.Address.City,
        warehouse.Address.Province,
        warehouse.Address.CountryCode,
        warehouse.Description,
        warehouse.CreatedAt,
        warehouse.UpdatedAt);

    private static string GetTypeLabel(WarehouseType type) => type switch
    {
        WarehouseType.Standard => "Standard",
        WarehouseType.Defective => "Fallati",
        WarehouseType.Consignment => "Conto Vendita",
        WarehouseType.Subcontractor => "Terzisti",
        WarehouseType.Transit => "Transito",
        _ => type.ToString()
    };

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();
}
