using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Validation;

/// <summary>
/// Validator for CreateProductRequest without reflection.
/// </summary>
public class CreateProductRequestValidator : IValidator<CreateProductRequest>
{
    public ValidationResult Validate(CreateProductRequest request)
    {
        var errors = new List<ValidationError>();

        // SupplierCode validation
        if (string.IsNullOrWhiteSpace(request.SupplierCode))
            errors.Add(new ValidationError(nameof(request.SupplierCode), "Supplier code is required"));
        else if (request.SupplierCode.Length > 100)
            errors.Add(new ValidationError(nameof(request.SupplierCode), "Supplier code must not exceed 100 characters"));

        // InternalCode validation
        if (string.IsNullOrWhiteSpace(request.InternalCode))
            errors.Add(new ValidationError(nameof(request.InternalCode), "Internal code is required"));
        else if (request.InternalCode.Length > 100)
            errors.Add(new ValidationError(nameof(request.InternalCode), "Internal code must not exceed 100 characters"));

        // Name validation
        if (string.IsNullOrWhiteSpace(request.Name))
            errors.Add(new ValidationError(nameof(request.Name), "Product name is required"));
        else if (request.Name.Length > 255)
            errors.Add(new ValidationError(nameof(request.Name), "Product name must not exceed 255 characters"));

        // DefaultMarkupRate validation
        if (request.DefaultMarkupRate is < 0 or > 9999)
            errors.Add(new ValidationError(nameof(request.DefaultMarkupRate), "Markup rate must be between 0 and 9999"));

        // VatRate validation
        if (request.VatRate is < 0 or > 100)
            errors.Add(new ValidationError(nameof(request.VatRate), "VAT rate must be between 0 and 100"));

        // Variants validation
        if (request.Variants.Count == 0)
            errors.Add(new ValidationError(nameof(request.Variants), "Product must have at least one variant"));
        else
        {
            var variantValidator = new CreateProductVariantRequestValidator();
            foreach (var (variant, index) in request.Variants.Select((v, i) => (v, i)))
            {
                var variantResult = variantValidator.Validate(variant);
                if (!variantResult.IsValid)
                {
                    foreach (var error in variantResult.Errors)
                    {
                        errors.Add(new ValidationError($"{nameof(request.Variants)}[{index}].{error.PropertyName}", error.Message));
                    }
                }
            }
        }

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }
}