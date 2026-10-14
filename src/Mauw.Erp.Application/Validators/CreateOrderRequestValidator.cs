using FluentValidation;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Validators;

/// <summary>
/// Validator for CreateOrderRequest.
/// </summary>
public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.ContactId)
            .NotEqual(Guid.Empty).WithMessage("Contact ID must be specified");

        RuleFor(x => x.DiscountRate)
            .GreaterThanOrEqualTo(0).WithMessage("Discount rate must be 0 or greater")
            .LessThanOrEqualTo(100).WithMessage("Discount rate must not exceed 100")
            .When(x => x.DiscountRate.HasValue);
    }
}