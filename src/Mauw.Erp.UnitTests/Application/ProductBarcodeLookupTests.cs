using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Infrastructure.DbContext;
using Mauw.Erp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.UnitTests.Application;

[Collection(PostgreSqlTestCollection.Name)]
public class ProductBarcodeLookupTests(PostgreSqlContainerFixture postgres)
{
    [Fact]
    public async Task SearchMatchesSupplierCodeAndCanRestrictResultsToSelectedSupplier()
    {
        await using var db = postgres.CreateDbContext();
        var supplierId = Guid.NewGuid();
        var otherSupplierId = Guid.NewGuid();
        var product = new Product(supplierId, "CODE-SUPPLIER-1", "BARCODE-1", "Prodotto 1", 0m, 22m, 10m);
        var otherProduct = new Product(otherSupplierId, "CODE-SUPPLIER-2", "BARCODE-2", "Prodotto 2", 0m, 22m, 10m);
        db.Products.AddRange(product, otherProduct);
        await db.SaveChangesAsync();

        var matchingVariant = new ProductVariant(product.Id, "BARCODE-VARIANT-1", 12.50m, supplierId,
            supplierCode: "VAR-CODE-1");
        var otherVariant = new ProductVariant(otherProduct.Id, "BARCODE-VARIANT-2", 19.25m, otherSupplierId,
            supplierCode: "VAR-CODE-2");
        db.ProductVariants.AddRange(matchingVariant, otherVariant);
        await db.SaveChangesAsync();

        var repository = new ProductVariantRepository(db);

        var productCodeResults = await repository.SearchAsync("CODE-SUPPLIER", 30, supplierId);
        var variantCodeResults = await repository.SearchAsync("VAR-CODE-1", 30, supplierId);
        var excludedSupplierResults = await repository.SearchAsync("VAR-CODE-2", 30, supplierId);

        Assert.Contains(productCodeResults, result => result.Id == matchingVariant.Id);
        Assert.Contains(variantCodeResults, result => result.Id == matchingVariant.Id);
        Assert.Empty(excludedSupplierResults);
        Assert.Equal(12.50m, matchingVariant.PurchaseCost);
    }

    [Fact]
    public async Task SimpleProductBarcodeResolvesItsSingleVariantWithoutChangingVariantBarcodeLookup()
    {
        await using var db = postgres.CreateDbContext();
        var supplierId = Guid.NewGuid();
        var barcode = "2901234567896";
        var simpleProduct = new Product(supplierId, "SUP-SIMPLE", barcode, "Prodotto semplice", 0m, 22m, 10m,
            hasVariants: false);
        var productWithVariants = new Product(supplierId, "SUP-VARIANTS", "2901234567889", "Prodotto variabile",
            0m, 22m, 10m);
        db.Products.AddRange(simpleProduct, productWithVariants);
        await db.SaveChangesAsync();
        var simpleVariant = new ProductVariant(simpleProduct.Id, "MNVR-LEGACY-SIMPLE", 10m, supplierId)
        {
            SellingPrice = 15m
        };
        var variant = new ProductVariant(productWithVariants.Id, "2901234567872", 10m, supplierId);
        db.ProductVariants.AddRange(simpleVariant, variant);
        await db.SaveChangesAsync();

        Assert.False(simpleProduct.HasVariants);
        Assert.Equal(barcode, simpleProduct.Barcode);
        Assert.Equal(simpleProduct.Id, await db.Products.AsNoTracking()
            .Where(product => !product.HasVariants && product.Barcode == barcode)
            .Select(product => product.Id)
            .FirstOrDefaultAsync());
        Assert.Equal(simpleVariant.Id, await db.ProductVariants.AsNoTracking()
            .Where(item => item.ProductId == simpleProduct.Id)
            .Select(item => item.Id)
            .FirstOrDefaultAsync());

        var repository = new ProductVariantRepository(db);

        var byProductBarcode = await repository.GetByBarcodeAsync(barcode);
        var byVariantBarcode = await repository.GetByBarcodeAsync(variant.Barcode);
        var simpleSearch = (await repository.SearchAsync(barcode)).ToList();
        var parentBarcodeLookup = await repository.GetByBarcodeAsync(productWithVariants.Barcode);

        Assert.Equal(simpleVariant.Id, byProductBarcode?.Id);
        Assert.Equal(variant.Id, byVariantBarcode?.Id);
        Assert.Contains(simpleSearch, result => result.Id == simpleVariant.Id);
        Assert.Null(parentBarcodeLookup);
        Assert.Equal(barcode, ProductMapper.MapVariantToDto(simpleVariant, simpleProduct).Data!.Barcode);
        Assert.Equal(barcode, ProductMapper.MapBarcodeVariantToDto(simpleVariant, simpleProduct).Data!.Barcode);
    }
}
