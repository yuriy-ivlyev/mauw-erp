using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Validation;

/// <summary>
/// Validator for CreateOrderRequest without reflection.
/// </summary>
public class CreateOrderRequestValidator : IValidator<CreateOrderRequest>
{
    public ValidationResult Validate(CreateOrderRequest request)
    {
        var errors = new List<ValidationError>();

        // ContactId validation
        if (request.ContactId == Guid.Empty)
            errors.Add(new(nameof(request.ContactId), "Contact ID is required"));

        // DiscountRate conditional validation
        if (request.DiscountRate.HasValue && (request.DiscountRate < 0 || request.DiscountRate > 100))
            errors.Add(new(nameof(request.DiscountRate), "Discount rate must be between 0 and 100"));

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }
}