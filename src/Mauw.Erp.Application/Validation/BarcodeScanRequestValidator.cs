using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Validation;

/// <summary>
/// Validator for BarcodeScanRequest without reflection.
/// </summary>
public class BarcodeScanRequestValidator : IValidator<BarcodeScanRequest>
{
    public ValidationResult Validate(BarcodeScanRequest request)
    {
        var errors = new List<ValidationError>();

        // Barcode validation
        if (string.IsNullOrWhiteSpace(request.Barcode))
            errors.Add(new(nameof(request.Barcode), "Barcode is required"));
        else if (request.Barcode.Length > 100)
            errors.Add(new(nameof(request.Barcode), "Barcode must not exceed 100 characters"));

        // Quantity validation
        if (request.Quantity == 0)
            errors.Add(new(nameof(request.Quantity), "Quantity cannot be zero"));

        // Type validation (must be in valid enum range)
        if (request.Type < 1 || request.Type > 3)
            errors.Add(new(nameof(request.Type), "Stock movement type is invalid"));

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure(errors);
    }
}