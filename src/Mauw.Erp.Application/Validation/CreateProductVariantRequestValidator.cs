using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Validation;

/// <summary>
/// Validator for CreateProductVariantRequest without reflection.
/// </summary>
public class CreateProductVariantRequestValidator : IValidator<CreateProductVariantRequest>
{
    public ValidationResult Validate(CreateProductVariantRequest request)
    {
        var errors = new List<ValidationError>();

        // ProductId validation
        if (request.ProductId == Guid.Empty)
            errors.Add(new(nameof(request.ProductId), "Product ID is required"));

        // ColorId validation (optional)
        if (!string.IsNullOrWhiteSpace(request.ColorId) && request.ColorId.Length > 3)
            errors.Add(new(nameof(request.ColorId), "Color ID must not exceed 3 characters"));

        // SizeId validation (optional)
        if (!string.IsNullOrWhiteSpace(request.SizeId) && request.SizeId.Length > 5)
            errors.Add(new(nameof(request.SizeId), "Size ID must not exceed 5 characters"));

        // Barcode validation
        if (string.IsNullOrWhiteSpace(request.Barcode))
            errors.Add(new(nameof(request.Barcode), "Barcode is required"));
        else if (request.Barcode.Length > 100)
            errors.Add(new(nameof(request.Barcode), "Barcode must not exceed 100 characters"));

        // PurchaseCost validation
        if (request.PurchaseCost < 0)
            errors.Add(new(nameof(request.PurchaseCost), "Purchase cost must be greater than or equal to 0"));
        var pricing = new Mauw.Erp.Domain.Services.CatalogPriceCalculator()
            .Validate(request.SellingPrice);
        if (!pricing.Success)
            errors.Add(new(nameof(request.SellingPrice), pricing.Error));

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }
}