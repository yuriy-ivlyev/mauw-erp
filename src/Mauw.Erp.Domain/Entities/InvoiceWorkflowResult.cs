namespace Mauw.Erp.Domain.Entities;

public sealed record InvoiceWorkflowResult(bool Success, string? Error = null)
{
    public static InvoiceWorkflowResult Ok() => new(true);

    public static InvoiceWorkflowResult Fail(string error) => new(false, error);
}
