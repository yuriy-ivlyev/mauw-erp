using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Validation;

/// <summary>
/// Validator for CreateContactRequest without reflection.
/// </summary>
public class CreateContactRequestValidator : IValidator<CreateContactRequest>
{
    public ValidationResult Validate(CreateContactRequest request)
    {
        var errors = new List<ValidationError>();

        // Type validation (must be in valid enum range)
        if (request.Type < 1 || request.Type > 3)
            errors.Add(new ValidationError(nameof(request.Type), "Contact type is invalid"));

        // BusinessName validation
        if (string.IsNullOrWhiteSpace(request.BusinessName))
            errors.Add(new ValidationError(nameof(request.BusinessName), "Business name is required"));
        else if (request.BusinessName.Length > 255)
            errors.Add(new ValidationError(nameof(request.BusinessName), "Business name must not exceed 255 characters"));

        // VatNumber validation
        if (string.IsNullOrWhiteSpace(request.VatNumber))
            errors.Add(new ValidationError(nameof(request.VatNumber), "VAT number is required"));
        else if (request.VatNumber.Length > 50)
            errors.Add(new ValidationError(nameof(request.VatNumber), "VAT number must not exceed 50 characters"));

        // DefaultDiscountRate validation
        if (request.DefaultDiscountRate < 0 || request.DefaultDiscountRate > 100)
            errors.Add(new ValidationError(nameof(request.DefaultDiscountRate), "Default discount rate must be between 0 and 100"));

        // DefaultCommissionRate validation
        if (request.DefaultCommissionRate < 0 || request.DefaultCommissionRate > 100)
            errors.Add(new ValidationError(nameof(request.DefaultCommissionRate), "Default commission rate must be between 0 and 100"));

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }
}