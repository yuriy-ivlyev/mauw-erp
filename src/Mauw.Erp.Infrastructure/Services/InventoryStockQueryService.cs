using System.Linq.Expressions;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Enums;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Infrastructure.Services;

public sealed class InventoryStockQueryService(MauwDbContext db) : IInventoryStockQueryService
{
    private const string AllocationNote =
        "Gli ordini senza deposito associato sono attribuiti al deposito attivo predefinito o, in sua assenza, al primo deposito attivo in ordine alfabetico.";

    private sealed record WarehouseInfo(Guid Id, string Code, string Name);
    private sealed class StockQueryRow
    {
        public Guid WarehouseId { get; init; }
        public string WarehouseCode { get; init; } = null!;
        public string WarehouseName { get; init; } = null!;
        public Guid VariantId { get; init; }
        public string Sku { get; init; } = null!;
        public string Barcode { get; init; } = null!;
        public string Description { get; init; } = null!;
        public string? Color { get; init; }
        public string? Size { get; init; }
        public string? Category { get; init; }
        public decimal QuantityOnHand { get; init; }
        public decimal QuantityReserved { get; init; }
        public decimal QuantityIncoming { get; init; }
        public decimal QuantityAvailable { get; init; }
        public decimal UnitCost { get; init; }
        public decimal TotalValue { get; init; }
        public string CostSource { get; init; } = null!;

        public InventoryStockDto ToDto() => new(
            WarehouseId,
            WarehouseCode,
            WarehouseName,
            VariantId,
            Sku,
            Barcode,
            Description,
            Color,
            Size,
            Category,
            QuantityOnHand,
            QuantityReserved,
            QuantityIncoming,
            QuantityAvailable,
            "pz",
            UnitCost,
            TotalValue,
            CostSource);
    }

    public async Task<OperationResult<InventoryStocksResponseDto>> GetStocksAsync(
        InventoryStockFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var rowsResult = await QueryRowsAsync(request, cancellationToken);
        if (!rowsResult.Success)
            return OperationResult<InventoryStocksResponseDto>.Fail(rowsResult.Error!);

        var rows = rowsResult.Data!;
        var summary = await CalculateSummaryAsync(rows, cancellationToken);
        var totalCount = summary.TotalCount;
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var totalPages = totalCount == 0 ? 0 : (totalCount + pageSize - 1) / pageSize;
        var page = totalPages == 0 ? 1 : Math.Clamp(request.Page, 1, totalPages);
        var items = (await OrderRows(rows, request.SortBy, request.SortDescending)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken))
            .Select(row => row.ToDto())
            .ToList();

        return OperationResult<InventoryStocksResponseDto>.Ok(new(
            items,
            page,
            pageSize,
            totalCount,
            totalPages,
            summary.Totals,
            DateTime.UtcNow,
            NormalizeUtc(request.AtDate),
            AllocationNote));
    }

    public async Task<OperationResult<InventoryStocksPageDto>> GetStocksPageAsync(
        InventoryStockFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var rowsResult = await QueryRowsAsync(request, cancellationToken);
        if (!rowsResult.Success)
            return OperationResult<InventoryStocksPageDto>.Fail(rowsResult.Error!);

        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var maximumPage = int.MaxValue / pageSize;
        if (maximumPage < int.MaxValue)
            maximumPage++;
        var page = Math.Clamp(request.Page, 1, maximumPage);
        var items = (await OrderRows(rowsResult.Data!, request.SortBy, request.SortDescending)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken))
            .Select(row => row.ToDto())
            .ToList();

        return OperationResult<InventoryStocksPageDto>.Ok(new(
            items,
            page,
            pageSize,
            DateTime.UtcNow,
            NormalizeUtc(request.AtDate),
            AllocationNote));
    }

    public async Task<OperationResult<InventoryStocksSummaryDto>> GetStocksSummaryAsync(
        InventoryStockFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var rowsResult = await QueryRowsAsync(request, cancellationToken);
        if (!rowsResult.Success)
            return OperationResult<InventoryStocksSummaryDto>.Fail(rowsResult.Error!);

        var summary = await CalculateSummaryAsync(rowsResult.Data!, cancellationToken);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var totalPages = summary.TotalCount == 0
            ? 0
            : (summary.TotalCount + pageSize - 1) / pageSize;

        return OperationResult<InventoryStocksSummaryDto>.Ok(new(
            summary.TotalCount,
            pageSize,
            totalPages,
            summary.Totals));
    }

    public async Task<OperationResult<InventoryStockExportDto>> GetExportAsync(
        InventoryStockFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var rowsResult = await QueryRowsAsync(request, cancellationToken);
        if (!rowsResult.Success)
            return OperationResult<InventoryStockExportDto>.Fail(rowsResult.Error!);

        var rows = rowsResult.Data!;
        var summary = await CalculateSummaryAsync(rows, cancellationToken);
        var items = (await OrderRows(rows, request.SortBy, request.SortDescending)
                .ToListAsync(cancellationToken))
            .Select(row => row.ToDto())
            .ToList();
        var warehouseName = request.WarehouseId.HasValue
            ? await db.Warehouses.AsNoTracking()
                .Where(warehouse => warehouse.Id == request.WarehouseId.Value)
                .Select(warehouse => warehouse.Name)
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        var categoryName = request.CategoryId.HasValue
            ? await db.Categories.AsNoTracking()
                .Where(category => category.Id == request.CategoryId.Value)
                .Select(category => category.Name)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return OperationResult<InventoryStockExportDto>.Ok(new(
            items,
            summary.Totals,
            DateTime.UtcNow,
            NormalizeUtc(request.AtDate),
            AllocationNote,
            request.ValuationMethod,
            new InventoryStockFilterSummaryDto(
                string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim(),
                warehouseName,
                categoryName,
                request.OnlyInStock)));
    }

    public async Task<OperationResult<InventoryStockMovementsResponseDto>> GetMovementsAsync(
        Guid warehouseId,
        Guid productVariantId,
        DateTime? atDate,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var cutoff = NormalizeUtc(atDate);
        var query = db.StockMovements.AsNoTracking()
            .Where(m => m.WarehouseId == warehouseId && m.VariantId == productVariantId);
        if (cutoff.HasValue)
            query = query.Where(m => m.MovementDateUtc <= cutoff.Value);

        var movements = await query
            .OrderBy(m => m.MovementDateUtc)
            .ThenBy(m => m.Id)
            .ToListAsync(cancellationToken);

        decimal runningBalance = 0m;
        var balanceKnown = true;
        var withBalances = movements.Select(m =>
        {
            var quantityDelta = m.GetEffectiveQuantityDelta();
            if (!quantityDelta.HasValue)
                balanceKnown = false;
            else if (balanceKnown)
                runningBalance += quantityDelta.Value;

            return new InventoryStockMovementDto(
                m.Id,
                m.MovementDateUtc,
                GetMovementTypeLabel(m.Type),
                StockService.GetReasonLabel(m.Reason),
                m.Quantity,
                quantityDelta,
                m.UnitCost,
                m.ReferenceDoc,
                m.Notes,
                balanceKnown ? runningBalance : null);
        }).Reverse().ToList();

        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var totalCount = withBalances.Count;
        var totalPages = totalCount == 0 ? 0 : (totalCount + safePageSize - 1) / safePageSize;
        var safePage = totalPages == 0 ? 1 : Math.Clamp(page, 1, totalPages);
        var items = withBalances
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToList();

        return OperationResult<InventoryStockMovementsResponseDto>.Ok(new(
            items,
            safePage,
            safePageSize,
            totalCount,
            totalPages));
    }

    public async Task<OperationResult<InventoryMovementReconciliationResponseDto>> GetAmbiguousMovementsAsync(
        Guid? warehouseId = null,
        Guid? productVariantId = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = db.StockMovements
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(movement => movement.Variant)
                .ThenInclude(variant => variant.Product)
            .Include(movement => movement.Warehouse)
            .Where(movement => !movement.QuantityDelta.HasValue);

        if (warehouseId.HasValue)
            query = query.Where(movement => movement.WarehouseId == warehouseId.Value);
        if (productVariantId.HasValue)
            query = query.Where(movement => movement.VariantId == productVariantId.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var totalPages = totalCount == 0 ? 0 : (totalCount + safePageSize - 1) / safePageSize;
        var safePage = totalPages == 0 ? 1 : Math.Clamp(page, 1, totalPages);
        var pageMovements = await query
            .OrderBy(movement => movement.WarehouseId)
            .ThenBy(movement => movement.Variant.Product.Name)
            .ThenBy(movement => movement.MovementDateUtc)
            .ThenBy(movement => movement.Id)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        var pairKeys = pageMovements
            .Where(movement => movement.WarehouseId.HasValue)
            .Select(movement => (WarehouseId: movement.WarehouseId!.Value, VariantId: movement.VariantId))
            .ToHashSet();

        var knownMovements = new List<StockMovement>();
        var ambiguousCounts = new Dictionary<(Guid WarehouseId, Guid VariantId), int>();
        var stockQuantities = new Dictionary<(Guid WarehouseId, Guid VariantId), decimal>();
        if (pairKeys.Count > 0)
        {
            var unresolvedMovements = await db.StockMovements
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(movement =>
                    !movement.QuantityDelta.HasValue &&
                    movement.WarehouseId.HasValue)
                .Where(BuildWarehouseVariantPairFilter<StockMovement>(pairKeys))
                .ToListAsync(cancellationToken);
            ambiguousCounts = unresolvedMovements
                .Where(movement => !movement.GetEffectiveQuantityDelta().HasValue)
                .GroupBy(movement => (WarehouseId: movement.WarehouseId!.Value, VariantId: movement.VariantId))
                .ToDictionary(group => group.Key, group => group.Count());

            var candidateMovements = await db.StockMovements
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(BuildWarehouseVariantPairFilter<StockMovement>(pairKeys))
                .ToListAsync(cancellationToken);
            knownMovements = candidateMovements
                .Where(movement =>
                    movement.WarehouseId.HasValue &&
                    pairKeys.Contains((movement.WarehouseId.Value, movement.VariantId)) &&
                    movement.GetEffectiveQuantityDelta().HasValue)
                .ToList();

            var stockItems = await db.StockItems
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(BuildWarehouseVariantPairFilter<StockItem>(pairKeys))
                .Select(stock => new
                {
                    stock.WarehouseId,
                    VariantId = stock.ProductVariantId,
                    stock.QuantityOnHand
                })
                .ToListAsync(cancellationToken);
            stockQuantities = stockItems
                .Where(stock => pairKeys.Contains((stock.WarehouseId, stock.VariantId)))
                .ToDictionary(
                    stock => (stock.WarehouseId, stock.VariantId),
                    stock => stock.QuantityOnHand);
        }

        var knownQuantities = pairKeys.ToDictionary(key => key, _ => 0m);
        foreach (var movement in knownMovements)
        {
            var key = (movement.WarehouseId!.Value, movement.VariantId);
            knownQuantities[key] += movement.GetEffectiveQuantityDelta()!.Value;
        }

        var items = pageMovements.Select(movement =>
        {
            var directionKnown = movement.GetEffectiveQuantityDelta();
            var key = movement.WarehouseId.HasValue
                ? (movement.WarehouseId.Value, movement.VariantId)
                : ((Guid WarehouseId, Guid VariantId)?)null;
            decimal? stockQuantity = key.HasValue && stockQuantities.TryGetValue(key.Value, out var onHand)
                ? onHand
                : null;
            decimal? knownQuantity = key.HasValue ? knownQuantities[key.Value] : null;
            decimal? unexplainedQuantity = stockQuantity.HasValue && knownQuantity.HasValue
                ? stockQuantity.Value - knownQuantity.Value
                : null;
            decimal? proposedDelta = null;
            var status = GetReconciliationStatus(
                movement,
                directionKnown,
                key,
                stockQuantity,
                unexplainedQuantity,
                ambiguousCounts,
                out proposedDelta);

            return new AmbiguousInventoryMovementDto(
                movement.Id,
                movement.WarehouseId,
                movement.Warehouse?.Code,
                movement.Warehouse?.Name,
                movement.VariantId,
                movement.Variant.Barcode,
                movement.Variant.Product.Name,
                movement.Type.ToString(),
                movement.Reason?.ToString(),
                movement.Quantity,
                movement.MovementDateUtc,
                movement.ReferenceDoc,
                !directionKnown.HasValue,
                directionKnown,
                stockQuantity,
                knownQuantity,
                unexplainedQuantity,
                proposedDelta,
                status);
        }).ToArray();

        return OperationResult<InventoryMovementReconciliationResponseDto>.Ok(new(
            items,
            safePage,
            safePageSize,
            totalCount,
            totalPages,
            DateTime.UtcNow));
    }

    private static Expression<Func<T, bool>> BuildWarehouseVariantPairFilter<T>(
        IReadOnlyCollection<(Guid WarehouseId, Guid VariantId)> pairKeys)
    {
        var row = Expression.Parameter(typeof(T), "row");
        Expression warehouseId = Expression.Property(row,
            typeof(T) == typeof(StockMovement)
                ? nameof(StockMovement.WarehouseId)
                : nameof(StockItem.WarehouseId));
        if (warehouseId.Type == typeof(Guid))
            warehouseId = Expression.Convert(warehouseId, typeof(Guid?));
        var variantId = Expression.Property(row,
            typeof(T) == typeof(StockMovement)
                ? nameof(StockMovement.VariantId)
                : nameof(StockItem.ProductVariantId));

        Expression? predicate = null;
        foreach (var pair in pairKeys)
        {
            var matchesPair = Expression.AndAlso(
                Expression.Equal(warehouseId,
                    Expression.Constant((Guid?)pair.WarehouseId, typeof(Guid?))),
                Expression.Equal(variantId,
                    Expression.Constant(pair.VariantId)));
            predicate = predicate is null
                ? matchesPair
                : Expression.OrElse(predicate, matchesPair);
        }

        return Expression.Lambda<Func<T, bool>>(
            predicate ?? Expression.Constant(false),
            row);
    }

    private static string GetReconciliationStatus(
        StockMovement movement,
        decimal? directionKnown,
        (Guid WarehouseId, Guid VariantId)? key,
        decimal? stockQuantity,
        decimal? unexplainedQuantity,
        IReadOnlyDictionary<(Guid WarehouseId, Guid VariantId), int> ambiguousCounts,
        out decimal? proposedDelta)
    {
        proposedDelta = null;
        if (directionKnown.HasValue)
            return "Direzione deducibile dal tipo di movimento.";
        if (!key.HasValue)
            return "Deposito non disponibile: impossibile confrontare i saldi.";
        if (!stockQuantity.HasValue || !unexplainedQuantity.HasValue)
            return "Giacenza di magazzino non disponibile.";
        if (!ambiguousCounts.TryGetValue(key.Value, out var ambiguousCount) || ambiguousCount != 1)
            return "Sono presenti più movimenti a direzione ignota per questa coppia deposito/variante.";

        var quantity = Math.Abs((decimal)movement.Quantity);
        if (quantity > 0m && unexplainedQuantity.Value == quantity)
        {
            proposedDelta = quantity;
            return "Segno positivo proposto: il residuo coincide con la quantità.";
        }
        if (quantity > 0m && unexplainedQuantity.Value == -quantity)
        {
            proposedDelta = -quantity;
            return "Segno negativo proposto: il residuo coincide con la quantità.";
        }

        return "Nessuna proposta: il residuo non coincide con la quantità del movimento.";
    }

    private static string GetMovementTypeLabel(StockMovementType type) => type switch
    {
        StockMovementType.PurchaseReceipt => "Carico fornitore",
        StockMovementType.SalesDelivery => "Scarico vendita",
        StockMovementType.InventoryAdjustment => "Rettifica inventario",
        StockMovementType.CustomerReturn => "Reso cliente",
        StockMovementType.SupplierStockIn => "Carico fornitore",
        StockMovementType.SupplierStockReturn => "Reso fornitore",
        _ => "Movimento di magazzino"
    };

    private async Task<OperationResult<IQueryable<StockQueryRow>>> QueryRowsAsync(
        InventoryStockFilterRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.ValuationMethod))
            return OperationResult<IQueryable<StockQueryRow>>.Fail("Metodo di valorizzazione non valido.");

        var defaultWarehouse = await db.Warehouses.AsNoTracking()
            .Where(w => w.IsActive && !w.IsDeleted)
            .OrderByDescending(w => w.IsDefault)
            .ThenBy(w => w.Code)
            .Select(w => new WarehouseInfo(w.Id, w.Code, w.Name))
            .FirstOrDefaultAsync(cancellationToken);

        var variants = db.ProductVariants.AsNoTracking().AsQueryable();
        if (request.CategoryId.HasValue)
            variants = variants.Where(v => v.Product.CategoryId == request.CategoryId.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{EscapeLikePattern(request.Search.Trim())}%";
            variants = variants.Where(v =>
                EF.Functions.ILike(v.Barcode, pattern, "\\") ||
                (v.SupplierCode != null && EF.Functions.ILike(v.SupplierCode, pattern, "\\")) ||
                EF.Functions.ILike(v.Product.SupplierCode, pattern, "\\") ||
                EF.Functions.ILike(v.Product.Name, pattern, "\\"));
        }

        var stockCandidates =
            from stock in db.StockItems.AsNoTracking()
            join variant in variants on stock.ProductVariantId equals variant.Id
            where !request.WarehouseId.HasValue || stock.WarehouseId == request.WarehouseId.Value
            select new
            {
                WarehouseId = stock.WarehouseId,
                WarehouseCode = stock.Warehouse.Code,
                WarehouseName = stock.Warehouse.Name,
                VariantId = variant.Id,
                Sku = variant.SupplierCode ?? variant.Product.SupplierCode,
                Barcode = variant.Barcode,
                Description = variant.Product.Name,
                Color = variant.Color == null ? null : variant.Color.Name,
                Size = variant.Size == null ? null : variant.Size.Name,
                Category = variant.Product.Category == null ? null : variant.Product.Category.Name,
                QuantityOnHand = stock.QuantityOnHand,
                QuantityReserved = stock.QuantityReserved,
                PurchaseCost = variant.PurchaseCost
            };

        var fallbackWarehouse = defaultWarehouse ?? new WarehouseInfo(Guid.Empty, string.Empty, string.Empty);
        var allowPendingCandidates = defaultWarehouse is not null &&
            (!request.WarehouseId.HasValue || request.WarehouseId.Value == defaultWarehouse.Id);
        var confirmedOrderIds = db.Orders.AsNoTracking()
            .Where(order => order.Status == OrderStatus.Confirmed)
            .Select(order => order.Id);
        var orderedQuantities = db.OrderItems.AsNoTracking()
            .Where(item => confirmedOrderIds.Contains(item.OrderId))
            .GroupBy(item => item.VariantId)
            .Select(group => new
            {
                VariantId = group.Key,
                Quantity = group.Sum(item => (decimal)item.Quantity)
            });
        var shippedQuantities = db.DdtItems.AsNoTracking()
            .Where(item =>
                item.DdtDocument.OrderId.HasValue &&
                confirmedOrderIds.Contains(item.DdtDocument.OrderId.Value))
            .GroupBy(item => item.VariantId)
            .Select(group => new
            {
                VariantId = group.Key,
                Quantity = (decimal?)group.Sum(item => (decimal)item.Quantity)
            });
        var committedQuantities = allowPendingCandidates
            ? (from order in orderedQuantities
               join shipment in shippedQuantities
                   on order.VariantId equals shipment.VariantId into shipmentGroup
               from shipment in shipmentGroup.DefaultIfEmpty()
               let remaining = order.Quantity - (shipment.Quantity ?? 0m)
               where remaining > 0m
               select new
               {
                   WarehouseId = defaultWarehouse!.Id,
                   VariantId = order.VariantId,
                   Quantity = (decimal?)remaining
               })
            : db.ProductVariants.AsNoTracking()
                .Where(_ => false)
                .Select(variant => new
                {
                    WarehouseId = fallbackWarehouse.Id,
                    VariantId = variant.Id,
                    Quantity = (decimal?)null
                });
        var incomingQuantities = allowPendingCandidates
            ? db.SupplierPurchaseOrderLines.AsNoTracking()
                .Where(line =>
                    line.SupplierPurchaseOrder.Status == SupplierPurchaseOrderStatus.Confirmed ||
                    line.SupplierPurchaseOrder.Status == SupplierPurchaseOrderStatus.PartiallyReceived)
                .Where(line => line.QuantityOrdered > line.QuantityReceived)
                .GroupBy(line => line.ArticleVariantId)
                .Select(group => new
                {
                    WarehouseId = defaultWarehouse!.Id,
                    VariantId = group.Key,
                    Quantity = (decimal?)group.Sum(line => (decimal)(line.QuantityOrdered - line.QuantityReceived))
                })
            : db.ProductVariants.AsNoTracking()
                .Where(_ => false)
                .Select(variant => new
                {
                    WarehouseId = fallbackWarehouse.Id,
                    VariantId = variant.Id,
                    Quantity = (decimal?)null
                });
        var pendingVariantIds = committedQuantities
            .Select(quantity => quantity.VariantId)
            .Concat(incomingQuantities.Select(quantity => quantity.VariantId))
            .Distinct();
        var pendingCandidates =
            from variant in variants
            join pendingVariantId in pendingVariantIds on variant.Id equals pendingVariantId
            where allowPendingCandidates
            where !db.StockItems.AsNoTracking().Any(stock =>
                stock.WarehouseId == fallbackWarehouse.Id &&
                stock.ProductVariantId == variant.Id)
            select new
            {
                WarehouseId = fallbackWarehouse.Id,
                WarehouseCode = fallbackWarehouse.Code,
                WarehouseName = fallbackWarehouse.Name,
                VariantId = variant.Id,
                Sku = variant.SupplierCode ?? variant.Product.SupplierCode,
                Barcode = variant.Barcode,
                Description = variant.Product.Name,
                Color = variant.Color == null ? null : variant.Color.Name,
                Size = variant.Size == null ? null : variant.Size.Name,
                Category = variant.Product.Category == null ? null : variant.Product.Category.Name,
                QuantityOnHand = 0m,
                QuantityReserved = 0m,
                PurchaseCost = variant.PurchaseCost
            };
        var candidates = stockCandidates.Concat(pendingCandidates);

        var movementCutoff = NormalizeUtc(request.AtDate);
        var movementAggregates = movementCutoff.HasValue
            ? (from movement in db.StockMovements.AsNoTracking()
               join stock in db.StockItems.AsNoTracking()
                   on new { WarehouseId = movement.WarehouseId!.Value, movement.VariantId }
                   equals new { stock.WarehouseId, VariantId = stock.ProductVariantId }
               join variant in variants on stock.ProductVariantId equals variant.Id
               where movement.WarehouseId.HasValue &&
                     movement.MovementDateUtc > movementCutoff.Value &&
                     (!request.WarehouseId.HasValue || stock.WarehouseId == request.WarehouseId.Value)
               group movement by new { stock.WarehouseId, VariantId = stock.ProductVariantId }
               into movementGroup
               select new
               {
                   movementGroup.Key.WarehouseId,
                   movementGroup.Key.VariantId,
                   QuantityDelta = (decimal?)movementGroup.Sum(movement => movement.QuantityDelta ??
                       (movement.Quantity == 0
                           ? 0m
                           : movement.Type == StockMovementType.PurchaseReceipt ||
                             movement.Type == StockMovementType.CustomerReturn ||
                             movement.Type == StockMovementType.SupplierStockIn
                               ? Math.Abs((decimal)movement.Quantity)
                               : movement.Type == StockMovementType.SalesDelivery ||
                                 movement.Type == StockMovementType.SupplierStockReturn
                                   ? -Math.Abs((decimal)movement.Quantity)
                                   : movement.Type == StockMovementType.InventoryAdjustment &&
                                     movement.Quantity < 0
                                       ? movement.Quantity
                                       : 0m))
               })
            : db.StockItems.AsNoTracking()
                .Where(_ => false)
                .Select(stock => new
                {
                    stock.WarehouseId,
                    VariantId = stock.ProductVariantId,
                    QuantityDelta = (decimal?)null
                });

        var valuationCutoff = NormalizeUtc(request.AtDate) ?? DateTime.UtcNow;
        var costAggregates =
            from movement in db.StockMovements.AsNoTracking()
            join stock in db.StockItems.AsNoTracking()
                on new { WarehouseId = movement.WarehouseId!.Value, movement.VariantId }
                equals new { stock.WarehouseId, VariantId = stock.ProductVariantId }
            join variant in variants on stock.ProductVariantId equals variant.Id
            where movement.WarehouseId.HasValue &&
                  movement.MovementDateUtc <= valuationCutoff &&
                  movement.QuantityDelta.HasValue &&
                  movement.QuantityDelta.Value > 0m &&
                  movement.UnitCost > 0m &&
                  (!request.WarehouseId.HasValue || stock.WarehouseId == request.WarehouseId.Value)
            group movement by new { stock.WarehouseId, VariantId = stock.ProductVariantId }
            into movementGroup
            select new
            {
                movementGroup.Key.WarehouseId,
                movementGroup.Key.VariantId,
                Quantity = (decimal?)movementGroup.Sum(movement => movement.QuantityDelta!.Value),
                WeightedTotal = (decimal?)movementGroup.Sum(movement => movement.QuantityDelta!.Value * movement.UnitCost),
                LastUnitCost = (decimal?)movementGroup.OrderByDescending(movement => movement.MovementDateUtc)
                    .ThenByDescending(movement => movement.Id)
                    .Select(movement => movement.UnitCost)
                    .FirstOrDefault()
            };

        var hasAmbiguousHistoricalMovement = request.AtDate.HasValue &&
            await (
                from movement in db.StockMovements.AsNoTracking()
                join stock in db.StockItems.AsNoTracking()
                    on new { WarehouseId = movement.WarehouseId!.Value, movement.VariantId }
                    equals new { stock.WarehouseId, VariantId = stock.ProductVariantId }
                join variant in variants on stock.ProductVariantId equals variant.Id
                where movement.WarehouseId.HasValue &&
                      movement.MovementDateUtc > movementCutoff!.Value &&
                      !movement.QuantityDelta.HasValue &&
                      movement.Quantity != 0 &&
                      movement.Type != StockMovementType.PurchaseReceipt &&
                      movement.Type != StockMovementType.SalesDelivery &&
                      movement.Type != StockMovementType.CustomerReturn &&
                      movement.Type != StockMovementType.SupplierStockIn &&
                      movement.Type != StockMovementType.SupplierStockReturn &&
                      !(movement.Type == StockMovementType.InventoryAdjustment && movement.Quantity < 0) &&
                      (!request.WarehouseId.HasValue || stock.WarehouseId == request.WarehouseId.Value)
                select movement.Id)
            .AnyAsync(cancellationToken);
        if (hasAmbiguousHistoricalMovement)
        {
            return OperationResult<IQueryable<StockQueryRow>>.Fail(
                "Impossibile ricostruire la giacenza alla data richiesta: almeno un movimento successivo non conserva la direzione della rettifica. Registrare una rettifica correttiva prima di usare lo storico.");
        }

        var query =
            from candidate in candidates
            join movement in movementAggregates
                on new { candidate.WarehouseId, candidate.VariantId }
                equals new { movement.WarehouseId, movement.VariantId } into movementGroup
            from movement in movementGroup.DefaultIfEmpty()
            join cost in costAggregates
                on new { candidate.WarehouseId, candidate.VariantId }
                equals new { cost.WarehouseId, cost.VariantId } into costGroup
            from cost in costGroup.DefaultIfEmpty()
            join committed in committedQuantities
                on new { candidate.WarehouseId, candidate.VariantId }
                equals new { committed.WarehouseId, committed.VariantId } into committedGroup
            from committed in committedGroup.DefaultIfEmpty()
            join incoming in incomingQuantities
                on new { candidate.WarehouseId, candidate.VariantId }
                equals new { incoming.WarehouseId, incoming.VariantId } into incomingGroup
            from incoming in incomingGroup.DefaultIfEmpty()
            let quantityOnHand = candidate.QuantityOnHand -
                (request.AtDate.HasValue ? movement.QuantityDelta ?? 0m : 0m)
            let quantityReserved = candidate.QuantityReserved + (committed.Quantity ?? 0m)
            let quantityIncoming = incoming.Quantity ?? 0m
            let unitCost = !cost.Quantity.HasValue
                ? candidate.PurchaseCost
                : request.ValuationMethod == ValuationMethod.LastPurchaseCost
                    ? cost.LastUnitCost ?? 0m
                    : (cost.WeightedTotal ?? 0m) / (cost.Quantity ?? 1m)
            select new StockQueryRow
            {
                WarehouseId = candidate.WarehouseId,
                WarehouseCode = candidate.WarehouseCode,
                WarehouseName = candidate.WarehouseName,
                VariantId = candidate.VariantId,
                Sku = candidate.Sku,
                Barcode = candidate.Barcode,
                Description = candidate.Description,
                Color = candidate.Color,
                Size = candidate.Size,
                Category = candidate.Category,
                QuantityOnHand = quantityOnHand,
                QuantityReserved = quantityReserved,
                QuantityIncoming = quantityIncoming,
                QuantityAvailable = quantityOnHand - quantityReserved + quantityIncoming,
                // PostgreSQL ROUND(numeric, scale) rounds midpoint values away from zero.
                UnitCost = Math.Round(unitCost, 8),
                TotalValue = Math.Round(quantityOnHand * Math.Round(unitCost, 8), 2),
                CostSource = !cost.Quantity.HasValue
                    ? InventoryStockCostSources.PurchaseCost
                    : InventoryStockCostSources.StockMovements,
            };

        if (request.OnlyInStock)
            query = query.Where(row => row.QuantityOnHand > 0m);

        return OperationResult<IQueryable<StockQueryRow>>.Ok(query);
    }

    private static async Task<(int TotalCount, InventoryStockTotalsDto Totals)> CalculateSummaryAsync(
        IQueryable<StockQueryRow> rows,
        CancellationToken cancellationToken)
    {
        var values = rows.Select(row => new
        {
            row.QuantityOnHand,
            row.QuantityReserved,
            row.QuantityIncoming,
            row.QuantityAvailable,
            row.TotalValue
        });
        var totals = await values.GroupBy(_ => 1)
            .Select(group => new
            {
                Count = group.Count(),
                Physical = group.Sum(row => row.QuantityOnHand),
                Reserved = group.Sum(row => row.QuantityReserved),
                Incoming = group.Sum(row => row.QuantityIncoming),
                Available = group.Sum(row => row.QuantityAvailable),
                Value = group.Sum(row => row.TotalValue)
            })
            .SingleOrDefaultAsync(cancellationToken);

        return (totals?.Count ?? 0, new InventoryStockTotalsDto(
            totals?.Physical ?? 0m,
            totals?.Reserved ?? 0m,
            totals?.Incoming ?? 0m,
            totals?.Available ?? 0m,
            totals?.Value ?? 0m));
    }

    private static IOrderedQueryable<StockQueryRow> OrderRows(
        IQueryable<StockQueryRow> rows,
        string? sortBy,
        bool descending)
    {
        IOrderedQueryable<StockQueryRow> ordered = (sortBy?.Trim().ToLowerInvariant()) switch
        {
            "sku" => descending ? rows.OrderByDescending(row => row.Sku) : rows.OrderBy(row => row.Sku),
            "description" or "productname" => descending
                ? rows.OrderByDescending(row => row.Description)
                : rows.OrderBy(row => row.Description),
            "barcode" => descending ? rows.OrderByDescending(row => row.Barcode) : rows.OrderBy(row => row.Barcode),
            "color" => descending ? rows.OrderByDescending(row => row.Color) : rows.OrderBy(row => row.Color),
            "size" => descending ? rows.OrderByDescending(row => row.Size) : rows.OrderBy(row => row.Size),
            "warehouse" or "warehousecode" => descending
                ? rows.OrderByDescending(row => row.WarehouseCode)
                : rows.OrderBy(row => row.WarehouseCode),
            "quantityonhand" or "physical" => descending
                ? rows.OrderByDescending(row => row.QuantityOnHand)
                : rows.OrderBy(row => row.QuantityOnHand),
            "quantityreserved" or "reserved" => descending
                ? rows.OrderByDescending(row => row.QuantityReserved)
                : rows.OrderBy(row => row.QuantityReserved),
            "quantityincoming" or "incoming" => descending
                ? rows.OrderByDescending(row => row.QuantityIncoming)
                : rows.OrderBy(row => row.QuantityIncoming),
            "quantityavailable" or "available" => descending
                ? rows.OrderByDescending(row => row.QuantityAvailable)
                : rows.OrderBy(row => row.QuantityAvailable),
            "unitcost" => descending ? rows.OrderByDescending(row => row.UnitCost) : rows.OrderBy(row => row.UnitCost),
            "totalvalue" or "estimatedvalue" => descending
                ? rows.OrderByDescending(row => row.TotalValue)
                : rows.OrderBy(row => row.TotalValue),
            _ => descending
                ? rows.OrderByDescending(row => row.WarehouseCode).ThenByDescending(row => row.Description)
                : rows.OrderBy(row => row.WarehouseCode).ThenBy(row => row.Description)
        };

        return descending
            ? ordered.ThenByDescending(row => row.Sku).ThenByDescending(row => row.VariantId)
            : ordered.ThenBy(row => row.Sku).ThenBy(row => row.VariantId);
    }

    private static string EscapeLikePattern(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private static DateTime? NormalizeUtc(DateTime? value)
    {
        if (!value.HasValue)
            return null;

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }
}
