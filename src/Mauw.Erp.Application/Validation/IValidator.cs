namespace Mauw.Erp.Application.Validation;

/// <summary>
/// Generic validator interface without reflection.
/// </summary>
public interface IValidator<in T>
{
    ValidationResult Validate(T request);
}

/// <summary>
/// Validation result containing errors.
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; }
    public List<ValidationError> Errors { get; }

    public ValidationResult(bool isValid = true, List<ValidationError>? errors = null)
    {
        IsValid = isValid;
        Errors = errors ?? [];
    }

    public static ValidationResult Success() => new(true);
    public static ValidationResult Failure(params ValidationError[] errors) => new(false, errors.ToList());
    public static ValidationResult Failure(List<ValidationError> errors) => new(false, errors);
}

/// <summary>
/// Represents a single validation error.
/// </summary>
public class ValidationError(string propertyName, string message)
{
    public string PropertyName { get; } = propertyName;
    public string Message { get; } = message;
}
