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

        RuleFor(x => x.Barcode)
            .NotEmpty().WithMessage("Barcode is required")
            .MaximumLength(100).WithMessage("Barcode must not exceed 100 characters");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required")
            .MaximumLength(255).WithMessage("Product name must not exceed 255 characters");

        RuleFor(x => x.BasePurchaseCost)
            .GreaterThanOrEqualTo(0).WithMessage("Purchase cost must be non-negative");

        RuleFor(x => x.VatRate)
            .GreaterThanOrEqualTo(0).WithMessage("VAT rate must be 0 or greater")
            .LessThanOrEqualTo(100).WithMessage("VAT rate must not exceed 100");
        RuleFor(x => x).Custom((request, context) =>
        {
            if (request.HasVariants) return;
            var pricing = new Mauw.Erp.Domain.Services.CatalogPriceCalculator()
                .Validate(request.SellingPrice);
            if (!pricing.Success)
                context.AddFailure(nameof(request.SellingPrice), pricing.Error);
        });
    }
}