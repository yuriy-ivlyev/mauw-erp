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

/// <summary>
/// Validator for CreateContactRequest without reflection.
/// </summary>
public class CreateContactRequestValidator : IValidator<CreateContactRequest>
{
    public ValidationResult Validate(CreateContactRequest request)
    {
        var errors = new List<ValidationError>();

        // Type validation (must be in valid enum range)
        if (request.Type < 1 || request.Type > 3)
            errors.Add(new ValidationError(nameof(request.Type), "Contact type is invalid"));

        // BusinessName validation
        if (string.IsNullOrWhiteSpace(request.BusinessName))
            errors.Add(new ValidationError(nameof(request.BusinessName), "Business name is required"));
        else if (request.BusinessName.Length > 255)
            errors.Add(new ValidationError(nameof(request.BusinessName), "Business name must not exceed 255 characters"));

        // VatNumber validation
        if (string.IsNullOrWhiteSpace(request.VatNumber))
            errors.Add(new ValidationError(nameof(request.VatNumber), "VAT number is required"));
        else if (request.VatNumber.Length > 50)
            errors.Add(new ValidationError(nameof(request.VatNumber), "VAT number must not exceed 50 characters"));

        // DefaultDiscountRate validation
        if (request.DefaultDiscountRate < 0 || request.DefaultDiscountRate > 100)
            errors.Add(new ValidationError(nameof(request.DefaultDiscountRate), "Default discount rate must be between 0 and 100"));

        // DefaultCommissionRate validation
        if (request.DefaultCommissionRate < 0 || request.DefaultCommissionRate > 100)
            errors.Add(new ValidationError(nameof(request.DefaultCommissionRate), "Default commission rate must be between 0 and 100"));

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }
}

/// <summary>
/// Validator for UpdateContactRequest without reflection.
/// </summary>
public class UpdateContactRequestValidator : IValidator<UpdateContactRequest>
{
    public ValidationResult Validate(UpdateContactRequest request)
    {
        var errors = new List<ValidationError>();

        // BusinessName conditional validation
        if (!string.IsNullOrWhiteSpace(request.BusinessName) && request.BusinessName.Length > 255)
            errors.Add(new ValidationError(nameof(request.BusinessName), "Business name must not exceed 255 characters"));

        // VatNumber conditional validation
        if (!string.IsNullOrWhiteSpace(request.VatNumber) && request.VatNumber.Length > 50)
            errors.Add(new ValidationError(nameof(request.VatNumber), "VAT number must not exceed 50 characters"));

        // DefaultDiscountRate conditional validation
        if (request.DefaultDiscountRate.HasValue && (request.DefaultDiscountRate < 0 || request.DefaultDiscountRate > 100))
            errors.Add(new ValidationError(nameof(request.DefaultDiscountRate), "Default discount rate must be between 0 and 100"));

        // DefaultCommissionRate conditional validation
        if (request.DefaultCommissionRate.HasValue && (request.DefaultCommissionRate < 0 || request.DefaultCommissionRate > 100))
            errors.Add(new ValidationError(nameof(request.DefaultCommissionRate), "Default commission rate must be between 0 and 100"));

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }
}

/// <summary>
/// Validator for CreateOrderRequest without reflection.
/// </summary>
public class CreateOrderRequestValidator : IValidator<CreateOrderRequest>
{
    public ValidationResult Validate(CreateOrderRequest request)
    {
        var errors = new List<ValidationError>();

        // ContactId validation
        if (request.ContactId == Guid.Empty)
            errors.Add(new ValidationError(nameof(request.ContactId), "Contact ID is required"));

        // DiscountRate conditional validation
        if (request.DiscountRate.HasValue && (request.DiscountRate < 0 || request.DiscountRate > 100))
            errors.Add(new ValidationError(nameof(request.DiscountRate), "Discount rate must be between 0 and 100"));

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }
}

/// <summary>
/// Validator for CreateOrderItemRequest without reflection.
/// </summary>
public class CreateOrderItemRequestValidator : IValidator<CreateOrderItemRequest>
{
    public ValidationResult Validate(CreateOrderItemRequest request)
    {
        var errors = new List<ValidationError>();

        // VariantId validation
        if (request.VariantId == Guid.Empty)
            errors.Add(new ValidationError(nameof(request.VariantId), "Variant ID is required"));

        // Quantity validation
        if (request.Quantity <= 0)
            errors.Add(new ValidationError(nameof(request.Quantity), "Quantity must be greater than 0"));

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }
}

/// <summary>
/// Validator for BarcodeScanRequest without reflection.
/// </summary>
public class BarcodeScanRequestValidator : IValidator<BarcodeScanRequest>
{
    public ValidationResult Validate(BarcodeScanRequest request)
    {
        var errors = new List<ValidationError>();

        // Barcode validation
        if (string.IsNullOrWhiteSpace(request.Barcode))
            errors.Add(new ValidationError(nameof(request.Barcode), "Barcode is required"));
        else if (request.Barcode.Length > 100)
            errors.Add(new ValidationError(nameof(request.Barcode), "Barcode must not exceed 100 characters"));

        // Quantity validation
        if (request.Quantity == 0)
            errors.Add(new ValidationError(nameof(request.Quantity), "Quantity cannot be zero"));

        // Type validation (must be in valid enum range)
        if (request.Type < 1 || request.Type > 3)
            errors.Add(new ValidationError(nameof(request.Type), "Stock movement type is invalid"));

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }
}

/// <summary>
/// Validator for CreateInvoiceRequest without reflection.
/// </summary>
public class CreateInvoiceRequestValidator : IValidator<CreateInvoiceRequest>
{
    public ValidationResult Validate(CreateInvoiceRequest request)
    {
        var errors = new List<ValidationError>();

        // ContactId validation
        if (request.ContactId == Guid.Empty)
            errors.Add(new ValidationError(nameof(request.ContactId), "Contact ID is required"));

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }
}
