namespace Mauw.Erp.Application.Validation;

/// <summary>
/// Represents a single validation error.
/// </summary>
public class ValidationError(string propertyName, string message)
{
    public string PropertyName { get; } = propertyName;
    public string Message { get; } = message;
}