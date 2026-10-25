using Mauw.Erp.Application.Services;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Domain.Services;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

public class InventoryServiceTests
{
    [Fact]
    public async Task ScanBarcodeReturnsCombinedValidationErrorsWithoutRepositoryCalls()
    {
        var (service, unitOfWork, variants, movements, _) = CreateService();

        var result = await service.ScanBarcodeAsync(new BarcodeScanRequest());

        Assert.False(result.Success);
        Assert.Equal("Barcode: Barcode is required; Quantity: Quantity cannot be zero; Type: Stock movement type is invalid",
            result.Error);
        variants.Verify(repository => repository.GetByBarcodeAsync(It.IsAny<string>()), Times.Never);
        movements.Verify(repository => repository.AddAsync(It.IsAny<StockMovement>()), Times.Never);
        unitOfWork.Verify(work => work.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ScanBarcodeFailsWhenBarcodeDoesNotResolve()
    {
        var (service, unitOfWork, variants, movements, _) = CreateService();
        variants.Setup(repository => repository.GetByBarcodeAsync("missing"))
            .ReturnsAsync((ProductVariant?)null);

        var result = await service.ScanBarcodeAsync(new BarcodeScanRequest
        {
            Barcode = "missing",
            Quantity = 2,
            Type = 1
        });

        Assert.False(result.Success);
        Assert.Equal("Barcode not found", result.Error);
        movements.Verify(repository => repository.AddAsync(It.IsAny<StockMovement>()), Times.Never);
        unitOfWork.Verify(work => work.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ScanBarcodeCreatesMovementAndDefaultsMissingReferenceToEmptyString()
    {
        var (service, unitOfWork, variants, movements, _) = CreateService();
        var variant = NewVariant();
        variants.Setup(repository => repository.GetByBarcodeAsync(variant.Barcode))
            .ReturnsAsync(variant);
        StockMovement? addedMovement = null;
        movements.Setup(repository => repository.AddAsync(It.IsAny<StockMovement>()))
            .Callback<StockMovement>(movement => addedMovement = movement)
            .Returns(Task.CompletedTask);

        var result = await service.ScanBarcodeAsync(new BarcodeScanRequest
        {
            Barcode = variant.Barcode,
            Quantity = 3,
            Type = 1
        });

        Assert.True(result.Success);
        Assert.Equal(variant.Id, result.Data!.VariantId);
        Assert.Equal("SalesDelivery", result.Data.Type);
        Assert.Equal(3, result.Data.Quantity);
        Assert.Equal(12.34m, result.Data.UnitCost);
        Assert.Equal("", result.Data.ReferenceDoc);
        Assert.NotNull(addedMovement);
        Assert.Equal(StockMovementType.SalesDelivery, addedMovement.Type);
        Assert.Equal("", addedMovement.ReferenceDoc);
        unitOfWork.Verify(work => work.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetMovementsMapsPagedResults()
    {
        var (service, _, _, movements, _) = CreateService();
        var variant = NewVariant();
        var movement = new StockMovement(variant.Id, StockMovementType.PurchaseReceipt, 4, 9.5m, "PO-4");
        movements.Setup(repository => repository.GetByVariantIdPagedAsync(variant.Id, 2, 5))
            .ReturnsAsync((new[] { movement }.AsEnumerable(), 11));

        var result = await service.GetMovementsAsync(variant.Id, 2, 5);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.PageNumber);
        Assert.Equal(5, result.Data.PageSize);
        Assert.Equal(11, result.Data.TotalCount);
        var dto = Assert.Single(result.Data.Items);
        Assert.Equal(movement.Id, dto.Id);
        Assert.Equal(variant.Id, dto.VariantId);
        Assert.Equal("PurchaseReceipt", dto.Type);
        Assert.Equal(4, dto.Quantity);
        Assert.Equal(9.5m, dto.UnitCost);
        Assert.Equal("PO-4", dto.ReferenceDoc);
    }

    [Fact]
    public async Task GetVariantByBarcodeFailsForMissingVariantOrProduct()
    {
        var (service, _, variants, _, _) = CreateService();
        variants.SetupSequence(repository => repository.GetByBarcodeAsync(It.IsAny<string>()))
            .ReturnsAsync((ProductVariant?)null)
            .ReturnsAsync(NewVariant(withProduct: false));

        var missingVariant = await service.GetVariantByBarcodeAsync("missing");
        var missingProduct = await service.GetVariantByBarcodeAsync("orphan");

        Assert.False(missingVariant.Success);
        Assert.Equal("Variant not found", missingVariant.Error);
        Assert.False(missingProduct.Success);
        Assert.Equal("Variant not found", missingProduct.Error);
    }

    [Fact]
    public async Task GetVariantByBarcodeReturnsProductAndPriceDetails()
    {
        var (service, _, variants, _, _) = CreateService();
        var variant = NewVariant();
        variants.Setup(repository => repository.GetByBarcodeAsync(variant.Barcode))
            .ReturnsAsync(variant);

        var result = await service.GetVariantByBarcodeAsync(variant.Barcode);

        Assert.True(result.Success);
        Assert.Equal(variant.Barcode, result.Data!.Barcode);
        Assert.Equal(variant.Id, result.Data.VariantId);
        Assert.Equal(variant.ProductId, result.Data.ProductId);
        Assert.Equal("Test product", result.Data.ProductName);
        Assert.Equal(15m, result.Data.UnitNetPrice);
    }

    [Fact]
    public async Task GetVariantByBarcodeWithStockFailsWhenVariantOrProductIsMissing()
    {
        var (service, _, variants, _, _) = CreateService();
        variants.SetupSequence(repository => repository.GetByBarcodeAsync(It.IsAny<string>()))
            .ReturnsAsync((ProductVariant?)null)
            .ReturnsAsync(NewVariant(withProduct: false));

        var missingVariant = await service.GetVariantByBarcodeWithStockAsync("missing", Guid.NewGuid());
        var missingProduct = await service.GetVariantByBarcodeWithStockAsync("orphan", Guid.NewGuid());

        Assert.False(missingVariant.Success);
        Assert.Equal("Variant not found", missingVariant.Error);
        Assert.False(missingProduct.Success);
        Assert.Equal("Variant not found", missingProduct.Error);
    }

    [Fact]
    public async Task GetVariantByBarcodeWithStockReturnsAvailableQuantity()
    {
        var (service, unitOfWork, variants, _, stock) = CreateService();
        var variant = NewVariant();
        var warehouseId = Guid.NewGuid();
        variants.Setup(repository => repository.GetByBarcodeAsync(variant.Barcode))
            .ReturnsAsync(variant);
        stock.Setup(repository => repository.GetAvailableQtyAsync(warehouseId, variant.Id))
            .ReturnsAsync(7m);

        var result = await service.GetVariantByBarcodeWithStockAsync(variant.Barcode, warehouseId);

        Assert.True(result.Success);
        Assert.Equal(7m, result.Data!.QuantityAvailable);
        Assert.Equal(variant.Barcode, result.Data.Barcode);
        unitOfWork.Verify(work => work.Stock, Times.Once);
    }

    [Fact]
    public async Task GetVariantByBarcodeWithStockReturnsPricingFailure()
    {
        var (service, _, variants, _, _) = CreateService();
        var variant = NewVariant();
        variant.SellingPrice = null;
        variants.Setup(repository => repository.GetByBarcodeAsync(variant.Barcode))
            .ReturnsAsync(variant);

        var result = await service.GetVariantByBarcodeWithStockAsync(variant.Barcode, Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Equal("Prezzo di vendita netto mancante: completare il catalogo.", result.Error);
    }

    private static (InventoryService Service, Mock<IUnitOfWork> UnitOfWork,
        Mock<IProductVariantRepository> Variants, Mock<IStockMovementRepository> Movements,
        Mock<IStockRepository> Stock) CreateService()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var variants = new Mock<IProductVariantRepository>();
        var movements = new Mock<IStockMovementRepository>();
        var stock = new Mock<IStockRepository>();
        unitOfWork.SetupGet(work => work.ProductVariants).Returns(variants.Object);
        unitOfWork.SetupGet(work => work.StockMovements).Returns(movements.Object);
        unitOfWork.SetupGet(work => work.Stock).Returns(stock.Object);
        var service = new InventoryService(
            unitOfWork.Object,
            new BarcodeScanRequestValidator());
        return (service, unitOfWork, variants, movements, stock);
    }

    private static ProductVariant NewVariant(bool withProduct = true)
    {
        var product = new Product(Guid.NewGuid(), "SUP-1", "PRODUCT-1", "Test product",
            22m, 10m, hasVariants: true);
        var variant = new ProductVariant(product.Id, "VARIANT-1", 12.34m, product.SupplierId)
        {
            SellingPrice = 15m
        };
        if (withProduct)
        {
            variant.Product = product;
            product.Variants.Add(variant);
        }
        return variant;
    }
}
