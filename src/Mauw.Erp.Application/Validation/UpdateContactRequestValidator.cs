using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Validation;

/// <summary>
/// Validator for UpdateContactRequest (partial update).
/// </summary>
public class UpdateContactRequestValidator : IValidator<UpdateContactRequest>
{
    public ValidationResult Validate(UpdateContactRequest request)
    {
        var errors = new List<ValidationError>();

        if (request.BusinessName is { Length: 0 })
            errors.Add(new(nameof(request.BusinessName), "Business name cannot be empty"));
        else if (request.BusinessName is { Length: > 255 })
            errors.Add(new(nameof(request.BusinessName), "Business name must not exceed 255 characters"));

        if (request.TradeName is { Length: > 255 })
            errors.Add(new(nameof(request.TradeName), "Trade name must not exceed 255 characters"));

        if (request.VatNumber is { Length: > 20 })
            errors.Add(new(nameof(request.VatNumber), "VAT number must not exceed 20 characters"));

        if (request.TaxCode is { Length: > 16 })
            errors.Add(new(nameof(request.TaxCode), "Tax code must not exceed 16 characters"));

        if (request.SdiCode is { Length: > 7 })
            errors.Add(new(nameof(request.SdiCode), "SDI code must not exceed 7 characters"));

        if (request.CustomerProfile is not null)
        {
            if (request.CustomerProfile.FinancialDiscountRate is < 0 or > 100)
                errors.Add(new("CustomerProfile.FinancialDiscountRate", "Discount rate must be between 0 and 100"));
            if (request.CustomerProfile.CreditLimit < 0)
                errors.Add(new("CustomerProfile.CreditLimit", "Credit limit must be >= 0"));
        }

        if (request.AgentProfile?.DefaultCommissionRate is < 0 or > 100)
            errors.Add(new("AgentProfile.DefaultCommissionRate", "Commission rate must be between 0 and 100"));

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }
}