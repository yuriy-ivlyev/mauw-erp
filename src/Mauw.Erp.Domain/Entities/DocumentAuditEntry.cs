namespace Mauw.Erp.Domain.Entities;

public static class AuditDocumentTypes
{
    public const string Order = nameof(Order);
    public const string Invoice = nameof(Invoice);
}

public static class AuditActions
{
    public const string OrderReopenedForAdjustment = nameof(OrderReopenedForAdjustment);
    public const string OrderLinesAdjusted = nameof(OrderLinesAdjusted);
    public const string InvoiceSynchronizedFromOrder = nameof(InvoiceSynchronizedFromOrder);
}

/// <summary>
/// Append-only audit record of a document change: who did what and when.
/// </summary>
public class DocumentAuditEntry : Entity<Guid>
{
    public const int NoteMaxLength = 500;
    public const int DetailsMaxLength = 4000;

    public string DocumentType { get; private set; } = string.Empty;
    public Guid DocumentId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string Note { get; private set; } = string.Empty;
    public string? Details { get; private set; }
    public string? UserId { get; private set; }
    public string? UserName { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    public DocumentAuditEntry(
        string documentType,
        Guid documentId,
        string action,
        string note,
        string? userId,
        string? userName,
        string? details = null,
        DateTime? occurredAtUtc = null) : base(Guid.NewGuid())
    {
        if (string.IsNullOrWhiteSpace(documentType))
            throw new ArgumentException("Il tipo di documento è obbligatorio.", nameof(documentType));
        if (documentId == Guid.Empty)
            throw new ArgumentException("Il documento è obbligatorio.", nameof(documentId));
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("L'azione è obbligatoria.", nameof(action));
        if (string.IsNullOrWhiteSpace(note))
            throw new ArgumentException("La nota di audit è obbligatoria.", nameof(note));

        DocumentType = documentType;
        DocumentId = documentId;
        Action = action;
        Note = Truncate(note.Trim(), NoteMaxLength)!;
        Details = Truncate(details, DetailsMaxLength);
        UserId = Truncate(userId, 200);
        UserName = Truncate(userName, 200);
        OccurredAtUtc = occurredAtUtc ?? DateTime.UtcNow;
    }

    private DocumentAuditEntry() { } // For EF Core

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Length <= maxLength ? value : value[..maxLength];
}
