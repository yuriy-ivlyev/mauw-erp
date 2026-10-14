using FluentValidation;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Validators;

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