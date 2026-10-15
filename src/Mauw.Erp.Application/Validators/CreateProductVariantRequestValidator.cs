using FluentValidation;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Validators;

/// <summary>
/// Validator for CreateProductVariantRequest.
/// </summary>
public class CreateProductVariantRequestValidator : AbstractValidator<CreateProductVariantRequest>
{
    public CreateProductVariantRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Product ID is required");

        RuleFor(x => x.ColorId)
            .NotEmpty().WithMessage("Color ID is required");

        RuleFor(x => x.SizeId)
            .NotEmpty().WithMessage("Size ID is required");

        RuleFor(x => x.Barcode)
            .NotEmpty().WithMessage("Barcode is required")
            .MaximumLength(100).WithMessage("Barcode must not exceed 100 characters");

        RuleFor(x => x.PurchaseCost)
            .GreaterThanOrEqualTo(0).WithMessage("Purchase cost must be 0 or greater");
    }
}