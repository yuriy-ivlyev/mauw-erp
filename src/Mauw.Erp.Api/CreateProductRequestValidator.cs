using FluentValidation;
using Mauw.Erp.Contracts;

namespace Mauw.Erp.Api;

public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.SupplierId)
            .NotEmpty();

        RuleFor(x => x.SupplierCode)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.InternalCode)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.DefaultMarkupRate)
            .GreaterThanOrEqualTo(0m)
            .LessThanOrEqualTo(1000m);

        RuleFor(x => x.VatRate)
            .GreaterThanOrEqualTo(0m)
            .LessThanOrEqualTo(100m);

        RuleFor(x => x.Variants)
            .NotNull();

        RuleForEach(x => x.Variants).SetValidator(new CreateProductVariantRequestValidator());
    }
}

public class CreateProductVariantRequestValidator : AbstractValidator<CreateProductVariantRequest>
{
    public CreateProductVariantRequestValidator()
    {
        RuleFor(x => x.Size)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Color)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Barcode)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.PurchaseCost)
            .GreaterThanOrEqualTo(0m);

        RuleFor(x => x.StockQuantity)
            .GreaterThanOrEqualTo(0);
    }
}
