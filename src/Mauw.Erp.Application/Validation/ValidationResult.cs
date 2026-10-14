namespace Mauw.Erp.Application.Validation;

/// <summary>
/// Validation result containing errors.
/// </summary>
public class ValidationResult(bool isValid = true, List<ValidationError>? errors = null)
{
    public bool IsValid { get; } = isValid;
    public List<ValidationError> Errors { get; } = errors ?? [];

    public static ValidationResult Success() => new(true);
    public static ValidationResult Failure(params ValidationError[] errors) => new(false, errors.ToList());
    public static ValidationResult Failure(List<ValidationError> errors) => new(false, errors);
}