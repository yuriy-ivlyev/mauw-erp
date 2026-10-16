using FluentValidation;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Validators;

public class UpdateContactRequestValidator : AbstractValidator<UpdateContactRequest>
{
    public UpdateContactRequestValidator()
    {
        RuleFor(x => x.BusinessName)
            .MaximumLength(255).WithMessage("Business name must not exceed 255 characters")
            .When(x => !string.IsNullOrEmpty(x.BusinessName));

        RuleFor(x => x.VatNumber)
            .MaximumLength(20).WithMessage("VAT number must not exceed 20 characters")
            .When(x => !string.IsNullOrEmpty(x.VatNumber));

        RuleFor(x => x.TaxCode)
            .MaximumLength(16).WithMessage("Tax code must not exceed 16 characters")
            .When(x => !string.IsNullOrEmpty(x.TaxCode));

        RuleFor(x => x.SdiCode)
            .MaximumLength(7).WithMessage("SDI code must not exceed 7 characters")
            .When(x => !string.IsNullOrEmpty(x.SdiCode));
    }
}