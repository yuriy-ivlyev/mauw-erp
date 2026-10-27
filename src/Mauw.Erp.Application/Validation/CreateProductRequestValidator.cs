using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.Application.Validation;

/// <summary>
/// Validator for CreateProductRequest without reflection.
/// </summary>
public class CreateProductRequestValidator(CatalogPriceCalculator catalogPrices) : IValidator<CreateProductRequest>
{
    public ValidationResult Validate(CreateProductRequest request)
    {
        var errors = new List<ValidationError>();

        // SupplierCode validation
        if (string.IsNullOrWhiteSpace(request.SupplierCode))
            errors.Add(new(nameof(request.SupplierCode), "Supplier code is required"));
        else if (request.SupplierCode.Length > 100)
            errors.Add(new(nameof(request.SupplierCode), "Supplier code must not exceed 100 characters"));

        // Barcode validation
        if (string.IsNullOrWhiteSpace(request.Barcode))
            errors.Add(new(nameof(request.Barcode), "Barcode is required"));
        else if (request.Barcode.Length > 100)
            errors.Add(new(nameof(request.Barcode), "Barcode must not exceed 100 characters"));

        // Name validation
        if (string.IsNullOrWhiteSpace(request.Name))
            errors.Add(new(nameof(request.Name), "Product name is required"));
        else if (request.Name.Length > 255)
            errors.Add(new(nameof(request.Name), "Product name must not exceed 255 characters"));

        if (request.BasePurchaseCost < 0m)
            errors.Add(new(nameof(request.BasePurchaseCost), "Purchase cost must be non-negative"));

        // VatRate validation
        if (request.VatRate is < 0 or > 100)
            errors.Add(new(nameof(request.VatRate), "VAT rate must be between 0 and 100"));
        if (!request.HasVariants)
        {
            var pricing = catalogPrices.Validate(request.SellingPrice);
            if (!pricing.Success)
                errors.Add(new(nameof(request.SellingPrice), pricing.Error));
        }

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }
}