using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Validation;

/// <summary>
/// Validator for CreateOrderItemRequest without reflection.
/// </summary>
public class CreateOrderItemRequestValidator : IValidator<CreateOrderItemRequest>
{
    public ValidationResult Validate(CreateOrderItemRequest request)
    {
        var errors = new List<ValidationError>();

        // VariantId validation
        if (request.VariantId == Guid.Empty)
            errors.Add(new(nameof(request.VariantId), "Variant ID is required"));

        // Quantity validation
        if (request.Quantity == 0 || (!request.IsCustomerReturn && request.Quantity < 0))
            errors.Add(new(nameof(request.Quantity), "Quantity must be positive (nonzero for customer returns)"));

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }
}