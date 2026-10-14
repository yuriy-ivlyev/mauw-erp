using FluentValidation;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Validators;

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