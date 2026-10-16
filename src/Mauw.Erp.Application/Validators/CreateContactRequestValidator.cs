using FluentValidation;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Validators;

public class CreateContactRequestValidator : AbstractValidator<CreateContactRequest>
{
    public CreateContactRequestValidator()
    {
        RuleFor(x => x.BusinessName)
            .NotEmpty().WithMessage("Business name is required")
            .MaximumLength(255).WithMessage("Business name must not exceed 255 characters");

        RuleFor(x => x.VatNumber)
            .MaximumLength(20).WithMessage("VAT number must not exceed 20 characters")
            .When(x => x.VatNumber != null);

        RuleFor(x => x.TaxCode)
            .MaximumLength(16).WithMessage("Tax code must not exceed 16 characters")
            .When(x => x.TaxCode != null);

        RuleFor(x => x.SdiCode)
            .MaximumLength(7).WithMessage("SDI code must not exceed 7 characters")
            .When(x => x.SdiCode != null);
    }
}