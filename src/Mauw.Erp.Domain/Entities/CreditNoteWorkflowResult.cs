namespace Mauw.Erp.Domain.Entities;

public sealed record CreditNoteWorkflowResult(bool Success, string? Error = null)
{
    public static CreditNoteWorkflowResult Ok() => new(true);
    public static CreditNoteWorkflowResult Fail(string error) => new(false, error);
}
