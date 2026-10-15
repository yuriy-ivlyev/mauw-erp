using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Validation;

/// <summary>
/// Validator for CreateInvoiceRequest without reflection.
/// </summary>
public class CreateInvoiceRequestValidator : IValidator<CreateInvoiceRequest>
{
    public ValidationResult Validate(CreateInvoiceRequest request)
    {
        var errors = new List<ValidationError>();

        // ContactId validation
        if (request.ContactId == Guid.Empty)
            errors.Add(new(nameof(request.ContactId), "Contact ID is required"));

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }
}