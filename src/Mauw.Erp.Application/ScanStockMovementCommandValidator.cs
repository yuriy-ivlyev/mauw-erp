using FluentValidation;

namespace Mauw.Erp.Application;

public class ScanStockMovementCommandValidator : AbstractValidator<ScanStockMovementCommand>
{
    public ScanStockMovementCommandValidator()
    {
        RuleFor(x => x.Barcode)
            .NotEmpty();

        RuleFor(x => x.Quantity)
            .NotEqual(0)
            .WithMessage("Quantity must not be zero.");

        RuleFor(x => x.Type)
            .IsInEnum();

        RuleFor(x => x.ReferenceDoc)
            .NotEmpty();
    }
}
