using Mauw.Erp.Domain.Enums;

namespace Mauw.Erp.Domain.Entities;

public sealed class EmployeeDocument : Entity<Guid>
{
    public Guid EmployeeId { get; private set; }
    public Employee Employee { get; private set; } = null!;
    public EmployeeDocumentKind Kind { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? DocumentNumber { get; private set; }
    public string? IssuerOrProvider { get; private set; }
    public DateOnly? IssuedOn { get; private set; }
    public DateOnly? CompletedOn { get; private set; }
    public DateOnly? ExpiresOn { get; private set; }
    public string? Notes { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long FileLength { get; private set; }
    public string? CreatedByUserId { get; private set; }
    public Guid Version { get; private set; } = Guid.NewGuid();

    public EmployeeDocument() : base(Guid.NewGuid()) { }

    public EmployeeDocument(Guid employeeId, string? createdByUserId) : base(Guid.NewGuid())
    {
        EmployeeId = employeeId;
        CreatedByUserId = Normalize(createdByUserId);
    }

    public EmployeeOperationResult SetDetails(
        EmployeeDocumentKind kind,
        string? title,
        string? documentNumber,
        string? issuerOrProvider,
        DateOnly? issuedOn,
        DateOnly? completedOn,
        DateOnly? expiresOn,
        string? notes)
    {
        if (EmployeeId == Guid.Empty)
            return EmployeeOperationResult.Fail("Il dipendente è obbligatorio.");
        if (!Enum.IsDefined(kind))
            return EmployeeOperationResult.Fail("Tipo documento dipendente non valido.");
        if (string.IsNullOrWhiteSpace(title) || title.Length > 150)
            return EmployeeOperationResult.Fail("Il titolo è obbligatorio (massimo 150 caratteri).");
        if (documentNumber?.Length > 100 || issuerOrProvider?.Length > 150 || notes?.Length > 1000)
            return EmployeeOperationResult.Fail("I metadati del documento superano la lunghezza massima.");
        if (issuedOn.HasValue && expiresOn.HasValue && expiresOn.Value < issuedOn.Value)
            return EmployeeOperationResult.Fail("La scadenza non può precedere la data di rilascio.");
        if (completedOn.HasValue && expiresOn.HasValue && expiresOn.Value < completedOn.Value)
            return EmployeeOperationResult.Fail("La scadenza non può precedere il completamento del corso.");
        if (kind == EmployeeDocumentKind.IdentityDocument
            && string.IsNullOrWhiteSpace(documentNumber))
            return EmployeeOperationResult.Fail("Il numero del documento di identità è obbligatorio.");
        if (kind == EmployeeDocumentKind.SafetyTraining && !completedOn.HasValue)
            return EmployeeOperationResult.Fail("Indicare la data di completamento del corso.");

        Kind = kind;
        Title = title.Trim();
        DocumentNumber = Normalize(documentNumber);
        IssuerOrProvider = Normalize(issuerOrProvider);
        IssuedOn = issuedOn;
        CompletedOn = completedOn;
        ExpiresOn = expiresOn;
        Notes = Normalize(notes);
        Version = Guid.NewGuid();
        UpdateTimestamp();
        return EmployeeOperationResult.Ok();
    }

    public EmployeeOperationResult SetAttachment(
        string? storageKey,
        string? fileName,
        string? contentType,
        long length)
    {
        if (string.IsNullOrWhiteSpace(storageKey) || storageKey.Length > 500
            || string.IsNullOrWhiteSpace(fileName) || fileName.Length > 255
            || string.IsNullOrWhiteSpace(contentType) || contentType.Length > 100
            || length <= 0 || length > 20 * 1024 * 1024)
            return EmployeeOperationResult.Fail("Allegato non valido o superiore a 20 MB.");

        StorageKey = storageKey;
        FileName = Path.GetFileName(fileName.Trim());
        ContentType = contentType;
        FileLength = length;
        Version = Guid.NewGuid();
        UpdateTimestamp();
        return EmployeeOperationResult.Ok();
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
