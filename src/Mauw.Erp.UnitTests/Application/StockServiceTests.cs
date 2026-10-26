using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Exceptions;
using Mauw.Erp.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

public class StockServiceTests
{
    [Fact]
    public async Task ExecuteMovementValidatesInputsAndReferencedEntities()
    {
        var (service, _, warehouses, variants, stock) = CreateService();
        var warehouseId = Guid.NewGuid();
        var variantId = Guid.NewGuid();

        Assert.Equal("Invalid stock movement reason",
            (await service.ExecuteMovementAsync(new(warehouseId, variantId, -1, 1m))).Error);
        Assert.Equal("Quantity must be different from zero",
            (await service.ExecuteMovementAsync(new(warehouseId, variantId,
                (int)StockMovementReason.SupplierReceipt, 0m))).Error);
        Assert.Equal("La quantita di merce deve essere un numero intero.",
            (await service.ExecuteMovementAsync(new(warehouseId, variantId,
                (int)StockMovementReason.SupplierReceipt, 1.5m))).Error);
        Assert.Equal("Warehouse not found",
            (await service.ExecuteMovementAsync(ValidMovement(warehouseId, variantId))).Error);

        warehouses.Setup(repository => repository.GetByIdAsync(warehouseId)).ReturnsAsync(NewWarehouse());
        Assert.Equal("Variant not found",
            (await service.ExecuteMovementAsync(ValidMovement(warehouseId, variantId))).Error);
        variants.Verify(repository => repository.GetByIdAsync(variantId), Times.Once);
        stock.Verify(repository => repository.RecordMovementAsync(
            It.IsAny<StockMovement>(), It.IsAny<decimal>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteMovementRecordsMovementAndMapsStockItem()
    {
        var (service, work, warehouses, variants, stock) = CreateService();
        var warehouse = NewWarehouse();
        var variant = NewVariant();
        var stockItem = NewStockItem(warehouse, variant, 5m, 10m);
        StockMovement? recorded = null;
        warehouses.Setup(repository => repository.GetByIdAsync(warehouse.Id)).ReturnsAsync(warehouse);
        variants.Setup(repository => repository.GetByIdAsync(variant.Id)).ReturnsAsync(variant);
        stock.Setup(repository => repository.RecordMovementAsync(It.IsAny<StockMovement>(), 3m))
            .Callback<StockMovement, decimal>((movement, _) => recorded = movement)
            .Returns(Task.CompletedTask);
        stock.Setup(repository => repository.GetStockItemAsync(warehouse.Id, variant.Id)).ReturnsAsync(stockItem);
        work.Setup(unit => unit.SaveChangesAsync()).Returns(Task.CompletedTask);

        var result = await service.ExecuteMovementAsync(new(
            warehouse.Id, variant.Id, (int)StockMovementReason.SupplierReceipt, 3m,
            " received ", "PO-3", "user-1"));

        Assert.True(result.Success, result.Error);
        Assert.Equal(5m, result.Data!.QuantityOnHand);
        Assert.Equal(50m, result.Data.EstimatedValue);
        Assert.Equal(StockMovementReason.SupplierReceipt, recorded!.Reason);
        Assert.Equal(" received ", recorded.Notes);
        Assert.Equal("PO-3", recorded.ReferenceDoc);
        Assert.Equal("user-1", recorded.CreatedByUserId);
        work.Verify(unit => unit.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task ExecuteMovementReportsMissingStockAndPersistenceErrors()
    {
        var (service, work, warehouses, variants, stock) = CreateService();
        var warehouse = NewWarehouse();
        var variant = NewVariant();
        warehouses.Setup(repository => repository.GetByIdAsync(warehouse.Id)).ReturnsAsync(warehouse);
        variants.Setup(repository => repository.GetByIdAsync(variant.Id)).ReturnsAsync(variant);
        stock.Setup(repository => repository.GetStockItemAsync(warehouse.Id, variant.Id))
            .ReturnsAsync((StockItem?)null);
        work.Setup(unit => unit.SaveChangesAsync()).Returns(Task.CompletedTask);
        var missingStock = await service.ExecuteMovementAsync(
            ValidMovement(warehouse.Id, variant.Id));
        Assert.Equal("Stock item not found", missingStock.Error);

        stock.Setup(repository => repository.RecordMovementAsync(It.IsAny<StockMovement>(), 1m))
            .ThrowsAsync(new ConcurrencyException("Concurrent update"));
        var conflict = await service.ExecuteMovementAsync(ValidMovement(warehouse.Id, variant.Id));
        Assert.Equal("Concurrent update", conflict.Error);

        stock.Setup(repository => repository.RecordMovementAsync(It.IsAny<StockMovement>(), 1m))
            .ThrowsAsync(new InvalidOperationException());
        var failure = await service.ExecuteMovementAsync(ValidMovement(warehouse.Id, variant.Id));
        Assert.Equal("Unable to execute stock movement", failure.Error);
    }

    [Fact]
    public async Task StockSummaryNormalizesPagingAndMapsInventoryValue()
    {
        var (service, _, _, _, stock) = CreateService();
        var item = NewStockItem(NewWarehouse(), NewVariant(), 8m, 4.25m, reserved: 2m);
        stock.Setup(repository => repository.GetStockByWarehousePagedAsync(
                null, "roma", 1, 100, "name", true))
            .ReturnsAsync((new[] { item }.AsEnumerable(), 7));

        var result = await service.GetStockSummaryPagedAsync(new(
            search: "roma", page: 0, pageSize: 500, sortBy: "name", sortDescending: true));

        Assert.True(result.Success, result.Error);
        Assert.Equal(1, result.Data!.PageNumber);
        Assert.Equal(100, result.Data.PageSize);
        Assert.Equal(7, result.Data.TotalCount);
        var dto = Assert.Single(result.Data.Items);
        Assert.Equal(6m, dto.QuantityAvailable);
        Assert.Equal(34m, dto.EstimatedValue);
        Assert.Equal("Product", dto.ProductName);
    }

    [Fact]
    public async Task TransferValidatesWarehousesVariantAndAvailableQuantity()
    {
        var (service, _, warehouses, variants, stock) = CreateService();
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var variantId = Guid.NewGuid();

        Assert.Equal("Source and target warehouses must be different",
            (await service.TransferAsync(new(sourceId, sourceId, variantId, 1m))).Error);
        Assert.Equal("Quantity must be greater than zero",
            (await service.TransferAsync(new(sourceId, targetId, variantId, 0m))).Error);
        Assert.Equal("Source warehouse not found",
            (await service.TransferAsync(new(sourceId, targetId, variantId, 1m))).Error);

        warehouses.Setup(repository => repository.GetByIdAsync(sourceId)).ReturnsAsync(NewWarehouse());
        Assert.Equal("Target warehouse not found",
            (await service.TransferAsync(new(sourceId, targetId, variantId, 1m))).Error);

        warehouses.Setup(repository => repository.GetByIdAsync(targetId)).ReturnsAsync(NewWarehouse());
        Assert.Equal("Variant not found",
            (await service.TransferAsync(new(sourceId, targetId, variantId, 1m))).Error);

        variants.Setup(repository => repository.GetByIdAsync(variantId)).ReturnsAsync(NewVariant());
        stock.Setup(repository => repository.GetAvailableQtyAsync(sourceId, variantId)).ReturnsAsync(1m);
        Assert.Equal("Insufficient available stock",
            (await service.TransferAsync(new(sourceId, targetId, variantId, 2m))).Error);
    }

    [Fact]
    public async Task TransferRecordsBothSidesAndCommitsTransaction()
    {
        var (service, work, warehouses, variants, stock) = CreateService();
        var source = NewWarehouse();
        var target = NewWarehouse();
        var variant = NewVariant();
        warehouses.Setup(repository => repository.GetByIdAsync(source.Id)).ReturnsAsync(source);
        warehouses.Setup(repository => repository.GetByIdAsync(target.Id)).ReturnsAsync(target);
        variants.Setup(repository => repository.GetByIdAsync(variant.Id)).ReturnsAsync(variant);
        stock.Setup(repository => repository.GetAvailableQtyAsync(source.Id, variant.Id)).ReturnsAsync(5m);
        stock.Setup(repository => repository.RecordMovementAsync(It.IsAny<StockMovement>(), It.IsAny<decimal>()))
            .Returns(Task.CompletedTask);
        work.Setup(unit => unit.BeginTransactionAsync(It.IsAny<System.Data.IsolationLevel>()))
            .Returns(Task.CompletedTask);
        work.Setup(unit => unit.SaveChangesAsync()).Returns(Task.CompletedTask);
        work.Setup(unit => unit.CommitAsync()).Returns(Task.CompletedTask);

        var result = await service.TransferAsync(new(source.Id, target.Id, variant.Id, 3m, "spostamento"));

        Assert.True(result.Success, result.Error);
        stock.Verify(repository => repository.RecordMovementAsync(
            It.Is<StockMovement>(movement => movement.WarehouseId == source.Id && movement.QuantityDelta == -3m),
            -3m), Times.Once);
        stock.Verify(repository => repository.RecordMovementAsync(
            It.Is<StockMovement>(movement => movement.WarehouseId == target.Id && movement.QuantityDelta == 3m),
            3m), Times.Once);
        work.Verify(unit => unit.CommitAsync(), Times.Once);
        work.Verify(unit => unit.RollbackAsync(), Times.Never);
    }

    [Fact]
    public async Task TransferRollsBackOnConcurrencyAndUnexpectedErrors()
    {
        var (service, work, warehouses, variants, stock) = CreateService();
        var source = NewWarehouse();
        var target = NewWarehouse();
        var variant = NewVariant();
        warehouses.Setup(repository => repository.GetByIdAsync(source.Id)).ReturnsAsync(source);
        warehouses.Setup(repository => repository.GetByIdAsync(target.Id)).ReturnsAsync(target);
        variants.Setup(repository => repository.GetByIdAsync(variant.Id)).ReturnsAsync(variant);
        stock.Setup(repository => repository.GetAvailableQtyAsync(source.Id, variant.Id)).ReturnsAsync(5m);
        work.Setup(unit => unit.BeginTransactionAsync(It.IsAny<System.Data.IsolationLevel>()))
            .Returns(Task.CompletedTask);
        stock.Setup(repository => repository.RecordMovementAsync(It.IsAny<StockMovement>(), It.IsAny<decimal>()))
            .ThrowsAsync(new ConcurrencyException("Concurrent update"));
        work.Setup(unit => unit.RollbackAsync()).Returns(Task.CompletedTask);

        var conflict = await service.TransferAsync(new(source.Id, target.Id, variant.Id, 1m));
        Assert.Equal("Concurrent update", conflict.Error);
        work.Verify(unit => unit.RollbackAsync(), Times.Once);

        stock.Setup(repository => repository.RecordMovementAsync(It.IsAny<StockMovement>(), It.IsAny<decimal>()))
            .ThrowsAsync(new InvalidOperationException());
        var failure = await service.TransferAsync(new(source.Id, target.Id, variant.Id, 1m));
        Assert.Equal("Unable to transfer stock", failure.Error);
        work.Verify(unit => unit.RollbackAsync(), Times.Exactly(2));
    }

    [Fact]
    public async Task StockQueriesMapAvailableQuantitiesAndMovementDetails()
    {
        var (service, _, _, _, stock) = CreateService();
        var warehouse = NewWarehouse();
        var variant = NewVariant();
        var movement = new StockMovement(variant.Id, warehouse.Id, StockMovementReason.CustomerReturn,
            2m, "reso", documentReference: "R-1")
        {
            Variant = variant
        };
        SetNavigation(movement, nameof(StockMovement.Warehouse), warehouse);
        stock.Setup(repository => repository.GetAvailableQtyAsync(warehouse.Id, variant.Id)).ReturnsAsync(3m);
        stock.Setup(repository => repository.GetMovementsPagedAsync(warehouse.Id, 1, 100, "date", true))
            .ReturnsAsync((new[] { movement }.AsEnumerable(), 2));

        Assert.Equal(3m, (await service.GetAvailableQtyAsync(warehouse.Id, variant.Id)).Data);
        var result = await service.GetMovementsPagedAsync(warehouse.Id, 0, 500, "date", true);

        Assert.True(result.Success, result.Error);
        Assert.Equal(100, result.Data!.PageSize);
        Assert.Equal(2, result.Data.TotalCount);
        var dto = Assert.Single(result.Data.Items);
        Assert.Equal("123", dto.Barcode);
        Assert.Equal("Product", dto.ProductName);
        Assert.Equal("WH-1", dto.WarehouseCode);
        Assert.Equal("Reso Cliente", dto.ReasonLabel);
        Assert.Equal("R-1", dto.DocumentReference);
    }

    [Theory]
    [InlineData(StockMovementReason.SupplierReceipt, "Carico Fornitore")]
    [InlineData(StockMovementReason.ShowroomSale, "Vendita Cassa")]
    [InlineData(StockMovementReason.OrderShipment, "Spedizione Ordine")]
    [InlineData(StockMovementReason.InternalTransfer, "Trasferimento")]
    [InlineData(StockMovementReason.CustomerReturn, "Reso Cliente")]
    [InlineData(StockMovementReason.SupplierReturn, "Reso Fornitore")]
    [InlineData(StockMovementReason.InventoryAdjustment, "Rettifica Inventario")]
    public void ReasonLabelsAreStable(StockMovementReason reason, string expected) =>
        Assert.Equal(expected, StockService.GetReasonLabel(reason));

    [Fact]
    public void UnknownMovementReasonHasNoLabel() =>
        Assert.Null(StockService.GetReasonLabel(null));

    private static (StockService Service, Mock<IUnitOfWork> Work,
        Mock<IWarehouseRepository> Warehouses, Mock<IProductVariantRepository> Variants,
        Mock<IStockRepository> Stock) CreateService()
    {
        var work = new Mock<IUnitOfWork>();
        var warehouses = new Mock<IWarehouseRepository>();
        var variants = new Mock<IProductVariantRepository>();
        var stock = new Mock<IStockRepository>();
        work.SetupGet(unit => unit.Warehouses).Returns(warehouses.Object);
        work.SetupGet(unit => unit.ProductVariants).Returns(variants.Object);
        work.SetupGet(unit => unit.Stock).Returns(stock.Object);
        return (new StockService(work.Object, NullLogger<StockService>.Instance),
            work, warehouses, variants, stock);
    }

    private static CreateStockMovementCommand ValidMovement(Guid warehouseId, Guid variantId) =>
        new(warehouseId, variantId, (int)StockMovementReason.SupplierReceipt, 1m);

    private static Warehouse NewWarehouse()
    {
        var warehouse = new Warehouse("WH-1", "Magazzino", WarehouseType.Standard, Guid.NewGuid(), true);
        SetId(warehouse);
        return warehouse;
    }

    private static ProductVariant NewVariant()
    {
        var product = new Product(Guid.NewGuid(), "SUP", "SKU", "Product", 22m, 10m);
        return new ProductVariant(product.Id, "123", 10m, product.SupplierId) { Product = product };
    }

    private static StockItem NewStockItem(
        Warehouse warehouse, ProductVariant variant, decimal onHand, decimal? unitCost, decimal reserved = 0m)
    {
        var item = new StockItem(warehouse.Id, variant.Id, onHand, unitCost);
        SetNavigation(item, nameof(StockItem.Warehouse), warehouse);
        SetNavigation(item, nameof(StockItem.ProductVariant), variant);
        if (reserved > 0m)
            item.Reserve(reserved);
        return item;
    }

    private static void SetId(Entity<Guid> entity) =>
        typeof(Entity<Guid>).GetProperty(nameof(Entity<Guid>.Id))!.SetValue(entity, Guid.NewGuid());

    private static void SetNavigation(object entity, string property, object value) =>
        entity.GetType().GetProperty(property)!.SetValue(entity, value);
}
