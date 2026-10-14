namespace Mauw.Erp.Application.Services;

/// <summary>
/// Result type for operations without exceptions.
/// </summary>
public record OperationResult<T>(bool Success, T? Data, string? Error = null)
{
    public static OperationResult<T> Ok(T data) => new(true, data);
    public static OperationResult<T> Fail(string error) => new(false, default, error);
}