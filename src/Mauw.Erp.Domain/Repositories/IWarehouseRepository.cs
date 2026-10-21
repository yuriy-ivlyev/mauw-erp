namespace Mauw.Erp.Domain.Repositories;

using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;

public interface IWarehouseRepository : IRepository<Warehouse, Guid>
{
    Task<(IEnumerable<Warehouse> Items, int Total)> GetPagedAsync(
        int page, int pageSize, string? search, WarehouseType? type, bool? isActive, string? sortBy = null, bool sortDescending = false);
    Task<Warehouse?> GetByIdWithAddressAsync(Guid id);
    Task<Warehouse?> GetDefaultWarehouseAsync();
    Task<bool> CodeExistsAsync(string code, Guid? excludeId = null);
}
