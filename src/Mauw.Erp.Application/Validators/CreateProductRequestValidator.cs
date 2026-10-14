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