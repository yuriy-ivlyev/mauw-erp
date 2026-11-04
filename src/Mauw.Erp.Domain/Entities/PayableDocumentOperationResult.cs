namespace Mauw.Erp.Domain.Entities;

public sealed record PayableDocumentOperationResult(bool Success, string? Error = null)
{
    public static PayableDocumentOperationResult Ok() => new(true);
    public static PayableDocumentOperationResult Fail(string error) => new(false, error);
}
