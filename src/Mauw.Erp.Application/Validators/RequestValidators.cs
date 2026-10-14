using FluentValidation;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Validators;

/// <summary>
/// Validator for CreateProductRequest.
/// </summary>
public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.SupplierCode)
            .NotEmpty().WithMessage("Supplier code is required")
            .MaximumLength(100).WithMessage("Supplier code must not exceed 100 characters");

        RuleFor(x => x.InternalCode)
            .NotEmpty().WithMessage("Internal code is required")
            .MaximumLength(100).WithMessage("Internal code must not exceed 100 characters");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required")
            .MaximumLength(255).WithMessage("Product name must not exceed 255 characters");

        RuleFor(x => x.DefaultMarkupRate)
            .GreaterThanOrEqualTo(0).WithMessage("Markup rate must be 0 or greater")
            .LessThanOrEqualTo(9999).WithMessage("Markup rate is invalid");

        RuleFor(x => x.VatRate)
            .GreaterThanOrEqualTo(0).WithMessage("VAT rate must be 0 or greater")
            .LessThanOrEqualTo(100).WithMessage("VAT rate must not exceed 100");

        RuleFor(x => x.Variants)
            .NotEmpty().WithMessage("Product must have at least one variant")
            .ForEach(v => v.SetValidator(new CreateProductVariantRequestValidator()));
    }
}

/// <summary>
/// Validator for CreateProductVariantRequest.
/// </summary>
public class CreateProductVariantRequestValidator : AbstractValidator<CreateProductVariantRequest>
{
    public CreateProductVariantRequestValidator()
    {
        RuleFor(x => x.Size)
            .NotEmpty().WithMessage("Size is required")
            .MaximumLength(50).WithMessage("Size must not exceed 50 characters");

        RuleFor(x => x.Color)
            .NotEmpty().WithMessage("Color is required")
            .MaximumLength(50).WithMessage("Color must not exceed 50 characters");

        RuleFor(x => x.Barcode)
            .NotEmpty().WithMessage("Barcode is required")
            .MaximumLength(50).WithMessage("Barcode must not exceed 50 characters");

        RuleFor(x => x.PurchaseCost)
            .GreaterThanOrEqualTo(0).WithMessage("Purchase cost must be 0 or greater");

        RuleFor(x => x.StockQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("Stock quantity must be 0 or greater");
    }
}

/// <summary>
/// Validator for CreateContactRequest.
/// </summary>
public class CreateContactRequestValidator : AbstractValidator<CreateContactRequest>
{
    public CreateContactRequestValidator()
    {
        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Contact type is invalid");

        RuleFor(x => x.BusinessName)
            .NotEmpty().WithMessage("Business name is required")
            .MaximumLength(255).WithMessage("Business name must not exceed 255 characters");

        RuleFor(x => x.VatNumber)
            .NotEmpty().WithMessage("VAT number is required")
            .MaximumLength(50).WithMessage("VAT number must not exceed 50 characters");

        RuleFor(x => x.DefaultDiscountRate)
            .GreaterThanOrEqualTo(0).WithMessage("Discount rate must be 0 or greater")
            .LessThanOrEqualTo(100).WithMessage("Discount rate must not exceed 100");

        RuleFor(x => x.DefaultCommissionRate)
            .GreaterThanOrEqualTo(0).WithMessage("Commission rate must be 0 or greater")
            .LessThanOrEqualTo(100).WithMessage("Commission rate must not exceed 100");
    }
}

/// <summary>
/// Validator for UpdateContactRequest.
/// </summary>
public class UpdateContactRequestValidator : AbstractValidator<UpdateContactRequest>
{
    public UpdateContactRequestValidator()
    {
        RuleFor(x => x.BusinessName)
            .MaximumLength(255).WithMessage("Business name must not exceed 255 characters")
            .When(x => !string.IsNullOrEmpty(x.BusinessName));

        RuleFor(x => x.VatNumber)
            .MaximumLength(50).WithMessage("VAT number must not exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.VatNumber));

        RuleFor(x => x.DefaultDiscountRate)
            .GreaterThanOrEqualTo(0).WithMessage("Discount rate must be 0 or greater")
            .LessThanOrEqualTo(100).WithMessage("Discount rate must not exceed 100")
            .When(x => x.DefaultDiscountRate.HasValue);

        RuleFor(x => x.DefaultCommissionRate)
            .GreaterThanOrEqualTo(0).WithMessage("Commission rate must be 0 or greater")
            .LessThanOrEqualTo(100).WithMessage("Commission rate must not exceed 100")
            .When(x => x.DefaultCommissionRate.HasValue);
    }
}

/// <summary>
/// Validator for BarcodeScanRequest.
/// </summary>
public class BarcodeScanRequestValidator : AbstractValidator<BarcodeScanRequest>
{
    public BarcodeScanRequestValidator()
    {
        RuleFor(x => x.Barcode)
            .NotEmpty().WithMessage("Barcode is required");

        RuleFor(x => x.Quantity)
            .NotEqual(0).WithMessage("Quantity must not be zero");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Invalid movement type");
    }
}

/// <summary>
/// Validator for CreateOrderRequest.
/// </summary>
public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.ContactId)
            .NotEqual(Guid.Empty).WithMessage("Contact ID must be specified");

        RuleFor(x => x.DiscountRate)
            .GreaterThanOrEqualTo(0).WithMessage("Discount rate must be 0 or greater")
            .LessThanOrEqualTo(100).WithMessage("Discount rate must not exceed 100")
            .When(x => x.DiscountRate.HasValue);
    }
}

/// <summary>
/// Validator for CreateInvoiceRequest.
/// </summary>
public class CreateInvoiceRequestValidator : AbstractValidator<CreateInvoiceRequest>
{
    public CreateInvoiceRequestValidator()
    {
        RuleFor(x => x.ContactId)
            .NotEqual(Guid.Empty).WithMessage("Contact ID must be specified");
    }
}

/// <summary>
/// Validator for RecordPaymentRequest.
/// </summary>
public class RecordPaymentRequestValidator : AbstractValidator<RecordPaymentRequest>
{
    public RecordPaymentRequestValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Payment amount must be greater than 0");
    }
}
