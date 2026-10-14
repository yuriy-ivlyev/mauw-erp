namespace Mauw.Erp.Application.Validation;

/// <summary>
/// Generic validator interface without reflection.
/// </summary>
public interface IValidator<in T>
{
    ValidationResult Validate(T request);
}