using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Infrastructure.Repositories;

public class StockRepository(MauwDbContext dbContext) : IStockRepository
{
    public async Task<StockItem?> GetStockItemAsync(Guid warehouseId, Guid productVariantId)
    {
        return await dbContext.StockItems
            .Include(s => s.Warehouse)
            .Include(s => s.ProductVariant)
                .ThenInclude(v => v.Product)
            .Include(s => s.ProductVariant)
                .ThenInclude(v => v.Color)
            .Include(s => s.ProductVariant)
                .ThenInclude(v => v.Size)
            .FirstOrDefaultAsync(s =>
                s.WarehouseId == warehouseId &&
                s.ProductVariantId == productVariantId);
    }

    public async Task<StockItem> GetOrCreateStockItemAsync(Guid warehouseId, Guid productVariantId)
    {
        var stockItem = await dbContext.StockItems
            .FirstOrDefaultAsync(s =>
                s.WarehouseId == warehouseId &&
                s.ProductVariantId == productVariantId);

        if (stockItem is not null)
            return stockItem;

        stockItem = new StockItem(warehouseId, productVariantId);
        await dbContext.StockItems.AddAsync(stockItem);
        return stockItem;
    }

    public async Task<(IEnumerable<StockItem> Items, int Total)> GetStockByWarehousePagedAsync(
        Guid? warehouseId,
        string? search,
        int page,
        int pageSize,
        string? sortBy = null,
        bool sortDescending = false)
    {
        var query = dbContext.StockItems
            .Include(s => s.ProductVariant)
                .ThenInclude(v => v.Product)
            .Include(s => s.ProductVariant)
                .ThenInclude(v => v.Color)
            .Include(s => s.ProductVariant)
                .ThenInclude(v => v.Size)
            .Include(s => s.Warehouse)
            .AsNoTracking()
            .AsQueryable();

        if (warehouseId.HasValue)
            query = query.Where(s => s.WarehouseId == warehouseId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(s =>
                s.ProductVariant.Product.Name.ToLower().Contains(term) ||
                s.ProductVariant.Barcode.ToLower().Contains(term));
        }

        var total = await query.CountAsync();

        var orderedQuery = sortBy?.ToLowerInvariant() switch
        {
            "barcode" => sortDescending ? query.OrderByDescending(s => s.ProductVariant.Barcode) : query.OrderBy(s => s.ProductVariant.Barcode),
            "productname" => sortDescending ? query.OrderByDescending(s => s.ProductVariant.Product.Name) : query.OrderBy(s => s.ProductVariant.Product.Name),
            "color" => sortDescending
                ? query.OrderByDescending(s => s.ProductVariant.Color == null ? null : s.ProductVariant.Color.Name)
                : query.OrderBy(s => s.ProductVariant.Color == null ? null : s.ProductVariant.Color.Name),
            "size" => sortDescending
                ? query.OrderByDescending(s => s.ProductVariant.Size == null ? null : s.ProductVariant.Size.Name)
                : query.OrderBy(s => s.ProductVariant.Size == null ? null : s.ProductVariant.Size.Name),
            "colorsize" => sortDescending
                ? query.OrderByDescending(s => s.ProductVariant.Color == null ? null : s.ProductVariant.Color.Name)
                    .ThenByDescending(s => s.ProductVariant.Size == null ? null : s.ProductVariant.Size.Name)
                : query.OrderBy(s => s.ProductVariant.Color == null ? null : s.ProductVariant.Color.Name)
                    .ThenBy(s => s.ProductVariant.Size == null ? null : s.ProductVariant.Size.Name),
            "quantityonhand" => sortDescending ? query.OrderByDescending(s => s.QuantityOnHand) : query.OrderBy(s => s.QuantityOnHand),
            "quantityreserved" => sortDescending ? query.OrderByDescending(s => s.QuantityReserved) : query.OrderBy(s => s.QuantityReserved),
            "quantityavailable" => sortDescending
                ? query.OrderByDescending(s => s.QuantityOnHand - s.QuantityReserved)
                : query.OrderBy(s => s.QuantityOnHand - s.QuantityReserved),
            "unitcost" => sortDescending ? query.OrderByDescending(s => s.UnitCost) : query.OrderBy(s => s.UnitCost),
            "estimatedvalue" => sortDescending ? query.OrderByDescending(s => s.QuantityOnHand * (s.UnitCost ?? 0m)) : query.OrderBy(s => s.QuantityOnHand * (s.UnitCost ?? 0m)),
            "warehousecode" => sortDescending
                ? query.OrderByDescending(s => s.Warehouse.Code).ThenBy(s => s.ProductVariant.Product.Name)
                : query.OrderBy(s => s.Warehouse.Code).ThenBy(s => s.ProductVariant.Product.Name),
            _ => query.OrderBy(s => s.Warehouse.Code).ThenBy(s => s.ProductVariant.Product.Name)
        };
        var items = await orderedQuery
            .ThenBy(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task RecordMovementAsync(StockMovement movement, decimal deltaOnHand)
    {
        await dbContext.StockMovements.AddAsync(movement);

        if (!movement.WarehouseId.HasValue)
            return;

        var stockItem = await GetOrCreateStockItemAsync(movement.WarehouseId.Value, movement.VariantId);
        stockItem.ApplyMovement(deltaOnHand);
        // Non chiamare Update esplicitamente: EF change tracking gestisce automaticamente
        // sia le entità nuove (Added) che quelle esistenti (Unchanged → Modified).
        // Chiamare Update su un'entità Added la converte in Modified → 0 righe colpite → DbUpdateConcurrencyException.
    }

    public async Task<decimal> GetAvailableQtyAsync(Guid warehouseId, Guid productVariantId)
    {
        var stockItem = await dbContext.StockItems
            .AsNoTracking()
            .FirstOrDefaultAsync(s =>
                s.WarehouseId == warehouseId &&
                s.ProductVariantId == productVariantId);

        return stockItem?.QuantityAvailable ?? 0;
    }

    public async Task<(IEnumerable<StockMovement> Items, int Total)> GetMovementsPagedAsync(
        Guid? warehouseId, int page, int pageSize, string? sortBy = null, bool sortDescending = false)
    {
        var query = dbContext.StockMovements
            .Include(m => m.Variant)
                .ThenInclude(v => v.Product)
            .Include(m => m.Warehouse)
            .AsNoTracking()
            .AsQueryable();

        if (warehouseId.HasValue)
            query = query.Where(m => m.WarehouseId == warehouseId.Value);

        var total = await query.CountAsync();
        var orderedQuery = sortBy?.ToLowerInvariant() switch
        {
            "productname" => sortDescending ? query.OrderByDescending(m => m.Variant.Product.Name) : query.OrderBy(m => m.Variant.Product.Name),
            "barcode" => sortDescending ? query.OrderByDescending(m => m.Variant.Barcode) : query.OrderBy(m => m.Variant.Barcode),
            "warehousecode" => sortDescending
                ? query.OrderByDescending(m => m.Warehouse == null ? null : m.Warehouse.Code)
                : query.OrderBy(m => m.Warehouse == null ? null : m.Warehouse.Code),
            "reasonlabel" => sortDescending ? query.OrderByDescending(m => m.Reason) : query.OrderBy(m => m.Reason),
            "quantity" => sortDescending ? query.OrderByDescending(m => m.Quantity) : query.OrderBy(m => m.Quantity),
            "notes" => sortDescending ? query.OrderByDescending(m => m.Notes) : query.OrderBy(m => m.Notes),
            "movementdateutc" => sortDescending ? query.OrderByDescending(m => m.MovementDateUtc) : query.OrderBy(m => m.MovementDateUtc),
            _ => query.OrderByDescending(m => m.MovementDateUtc)
        };
        var items = await orderedQuery
            .ThenBy(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }
}
