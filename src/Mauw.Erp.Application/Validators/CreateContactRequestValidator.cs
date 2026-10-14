using FluentValidation;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Validators;

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