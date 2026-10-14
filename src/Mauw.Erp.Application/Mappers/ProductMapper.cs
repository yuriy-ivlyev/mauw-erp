using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.Application.Mappers;

/// <summary>
/// Manual mapper for Product entities to DTOs without reflection.
/// </summary>
public static class ProductMapper
{
    private static readonly PricingEngine PricingEngine = new();

    public static ProductDto MapToDto(Product product)
    {
        return new ProductDto(
            Id: product.Id,
            SupplierId: product.SupplierId,
            SupplierCode: product.SupplierCode,
            InternalCode: product.InternalCode,
            Name: product.Name,
            DefaultMarkupRate: product.DefaultMarkupRate,
            VatRate: product.VatRate,
            ImageUrl: product.ImageUrl,
            Variants: product.Variants.Select(v => MapVariantToDto(v, product.DefaultMarkupRate)).ToList()
        );
    }

    public static ProductVariantDto MapVariantToDto(ProductVariant variant, decimal defaultMarkupRate)
    {
        var pricingResult = PricingEngine.CalculatePrice(
            variant.PurchaseCost,
            defaultMarkupRate,
            0,
            0
        );

        return new ProductVariantDto(
            Id: variant.Id,
            ProductId: variant.ProductId,
            Size: variant.Size,
            Color: variant.Color,
            Barcode: variant.Barcode,
            PurchaseCost: variant.PurchaseCost,
            StockQuantity: variant.StockQuantity,
            ListPrice: pricingResult.ListPrice,
            FinalNetPrice: pricingResult.FinalNetPrice,
            VatAmount: 0,
            FinalGrossPrice: pricingResult.FinalNetPrice
        );
    }

    public static BarcodeVariantDto MapBarcodeVariantToDto(ProductVariant variant, Product product)
    {
        var pricingResult = PricingEngine.CalculatePrice(
            variant.PurchaseCost,
            product.DefaultMarkupRate,
            0,
            product.VatRate
        );

        return new BarcodeVariantDto(
            Barcode: variant.Barcode,
            VariantId: variant.Id,
            ProductId: variant.ProductId,
            ProductName: product.Name,
            Size: variant.Size,
            Color: variant.Color,
            StockQuantity: variant.StockQuantity,
            UnitNetPrice: pricingResult.FinalNetPrice,
            UnitGrossPrice: pricingResult.FinalGrossPrice,
            VatRate: product.VatRate
        );
    }

    public static List<ProductDto> MapToListDto(IEnumerable<Product> products)
    {
        return products.Select(MapToDto).ToList();
    }
}