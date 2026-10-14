using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Validation;

/// <summary>
/// Validator for UpdateContactRequest without reflection.
/// </summary>
public class UpdateContactRequestValidator : IValidator<UpdateContactRequest>
{
    public ValidationResult Validate(UpdateContactRequest request)
    {
        var errors = new List<ValidationError>();

        // BusinessName conditional validation
        if (!string.IsNullOrWhiteSpace(request.BusinessName) && request.BusinessName.Length > 255)
            errors.Add(new ValidationError(nameof(request.BusinessName), "Business name must not exceed 255 characters"));

        // VatNumber conditional validation
        if (!string.IsNullOrWhiteSpace(request.VatNumber) && request.VatNumber.Length > 50)
            errors.Add(new ValidationError(nameof(request.VatNumber), "VAT number must not exceed 50 characters"));

        // DefaultDiscountRate conditional validation
        if (request.DefaultDiscountRate.HasValue && (request.DefaultDiscountRate < 0 || request.DefaultDiscountRate > 100))
            errors.Add(new ValidationError(nameof(request.DefaultDiscountRate), "Default discount rate must be between 0 and 100"));

        // DefaultCommissionRate conditional validation
        if (request.DefaultCommissionRate.HasValue && (request.DefaultCommissionRate < 0 || request.DefaultCommissionRate > 100))
            errors.Add(new ValidationError(nameof(request.DefaultCommissionRate), "Default commission rate must be between 0 and 100"));

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }
}