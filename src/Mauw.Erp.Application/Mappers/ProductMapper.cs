using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Services;
using Mauw.Erp.Application.Services;

namespace Mauw.Erp.Application.Mappers;

/// <summary>
/// Manual mapper for Product entities to DTOs without reflection.
/// </summary>
public static class ProductMapper
{
    public static OperationResult<ProductDto> MapToDto(Product product, CatalogPriceCalculator catalogPrices)
    {
        var variants = new List<ProductVariantDto>();
        foreach (var variant in product.Variants)
        {
            var result = MapVariantToDto(variant, product, catalogPrices);
            if (!result.Success)
                return OperationResult<ProductDto>.Fail(result.Error!);
            variants.Add(result.Data!);
        }
        return OperationResult<ProductDto>.Ok(new ProductDto(
            Id: product.Id,
            SupplierId: product.SupplierId,
            SupplierCode: product.SupplierCode,
            Barcode: product.Barcode,
            Name: product.Name,
            SellingPrice: product.HasVariants ? null : product.SellingPrice,
            VatRate: product.VatRate,
            BasePurchaseCost: product.BasePurchaseCost,
            Variants: variants,
            HasVariants: product.HasVariants,
            CategoryId: product.CategoryId,
            CategoryName: product.Category?.Name,
            CollectionId: product.CollectionId,
            CollectionName: product.Collection?.Name,
            MaterialId: product.MaterialId,
            MaterialName: product.Material?.Name
        ));
    }

    public static OperationResult<ProductVariantDto> MapVariantToDto(
        ProductVariant variant,
        Product product,
        CatalogPriceCalculator catalogPrices)
    {
        if (variant.SellingPrice.HasValue)
        {
            var validation = catalogPrices.Validate(variant.SellingPrice);
            if (!validation.Success)
                return OperationResult<ProductVariantDto>.Fail(validation.Error);
        }
        var price = variant.SellingPrice;

        return OperationResult<ProductVariantDto>.Ok(new(
            Id: variant.Id,
            ProductId: variant.ProductId,
            ColorId: variant.ColorId,
            SizeId: variant.SizeId,
            Color: variant.Color?.Name,
            Size: variant.Size?.Name,
            Barcode: product.HasVariants ? variant.Barcode : product.Barcode,
            PurchaseCost: variant.PurchaseCost,
            StockQuantity: 0,
            ListPrice: price,
            FinalNetPrice: price,
            VatAmount: 0,
            FinalGrossPrice: price,
            WeightGrams: variant.WeightGrams,
            SellingPrice: variant.SellingPrice,
            VarianteVetrina: variant.VarianteVetrina,
            SupplierCode: variant.SupplierCode
        ));
    }

    public static OperationResult<BarcodeVariantDto> MapBarcodeVariantToDto(
        ProductVariant variant,
        Product product,
        CatalogPriceCalculator catalogPrices)
    {
        var pricing = catalogPrices.GetStoredPrice(variant);
        if (!pricing.Success)
            return OperationResult<BarcodeVariantDto>.Fail(pricing.Error);
        var price = pricing.Data!.Value;

        return OperationResult<BarcodeVariantDto>.Ok(new(
            Barcode: product.HasVariants ? variant.Barcode : product.Barcode,
            VariantId: variant.Id,
            ProductId: variant.ProductId,
            ProductName: product.Name,
            Size: variant.Size?.Name ?? "N/A",
            Color: variant.Color?.Name ?? "N/A",
            UnitNetPrice: price,
            VatRate: product.VatRate,
            WeightGrams: variant.WeightGrams
        ));
    }

    public static OperationResult<List<ProductDto>> MapToListDto(
        IEnumerable<Product> products,
        CatalogPriceCalculator catalogPrices)
    {
        var dtos = new List<ProductDto>();
        foreach (var product in products)
        {
            var result = MapToDto(product, catalogPrices);
            if (!result.Success)
                return OperationResult<List<ProductDto>>.Fail(result.Error!);
            dtos.Add(result.Data!);
        }
        return OperationResult<List<ProductDto>>.Ok(dtos);
    }
}