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
            errors.Add(new ValidationError(nameof(request.ProductId), "Product ID is required"));

        // ColorId validation
        if (request.ColorId == Guid.Empty)
            errors.Add(new ValidationError(nameof(request.ColorId), "Color ID is required"));

        // SizeId validation
        if (request.SizeId == Guid.Empty)
            errors.Add(new ValidationError(nameof(request.SizeId), "Size ID is required"));

        // Barcode validation
        if (string.IsNullOrWhiteSpace(request.Barcode))
            errors.Add(new ValidationError(nameof(request.Barcode), "Barcode is required"));
        else if (request.Barcode.Length > 100)
            errors.Add(new ValidationError(nameof(request.Barcode), "Barcode must not exceed 100 characters"));

        // PurchaseCost validation
        if (request.PurchaseCost < 0)
            errors.Add(new ValidationError(nameof(request.PurchaseCost), "Purchase cost must be greater than or equal to 0"));

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }
}