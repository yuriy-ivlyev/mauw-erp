using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Services;

public interface IWarehouseService
{
    Task<OperationResult<PagedResult<WarehouseSummaryDto>>> GetPagedAsync(
        int page, int pageSize, string? search, int? type, bool? isActive, string? sortBy = null, bool sortDescending = false);
    Task<OperationResult<WarehouseDetailDto>> GetByIdAsync(Guid id);
    Task<OperationResult<WarehouseDetailDto>> CreateAsync(CreateWarehouseRequest request);
    Task<OperationResult<WarehouseDetailDto>> UpdateAsync(Guid id, UpdateWarehouseRequest request);
    Task<OperationResult<bool>> DeleteAsync(Guid id);
    Task<OperationResult<WarehouseDetailDto>> SetDefaultAsync(Guid id);
}
