namespace Mauw.Erp.Domain.Accounting;

public sealed record AccountingResult<T> where T : class
{
    public bool Success { get; }
    public T? Data { get; }
    public string Error { get; }

    private AccountingResult(bool success, T? data, string error)
    {
        Success = success;
        Data = data;
        Error = error;
    }

    public static AccountingResult<T> Ok(T data) => new(true, data, string.Empty);

    public static AccountingResult<T> Fail(string error) => new(false, null, error);
}
