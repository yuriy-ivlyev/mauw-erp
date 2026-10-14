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

        // Size validation
        if (string.IsNullOrWhiteSpace(request.Size))
            errors.Add(new ValidationError(nameof(request.Size), "Size is required"));
        else if (request.Size.Length > 50)
            errors.Add(new ValidationError(nameof(request.Size), "Size must not exceed 50 characters"));

        // Color validation
        if (string.IsNullOrWhiteSpace(request.Color))
            errors.Add(new ValidationError(nameof(request.Color), "Color is required"));
        else if (request.Color.Length > 50)
            errors.Add(new ValidationError(nameof(request.Color), "Color must not exceed 50 characters"));

        // Barcode validation
        if (string.IsNullOrWhiteSpace(request.Barcode))
            errors.Add(new ValidationError(nameof(request.Barcode), "Barcode is required"));
        else if (request.Barcode.Length > 100)
            errors.Add(new ValidationError(nameof(request.Barcode), "Barcode must not exceed 100 characters"));

        // PurchaseCost validation
        if (request.PurchaseCost < 0)
            errors.Add(new ValidationError(nameof(request.PurchaseCost), "Purchase cost must be greater than or equal to 0"));

        // StockQuantity validation
        if (request.StockQuantity < 0)
            errors.Add(new ValidationError(nameof(request.StockQuantity), "Stock quantity must be greater than or equal to 0"));

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }
}