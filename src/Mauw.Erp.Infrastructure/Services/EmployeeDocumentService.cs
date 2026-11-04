using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Mauw.Erp.Infrastructure.Services;

public sealed class EmployeeDocumentService(
    MauwDbContext db,
    IEmployeeDocumentStorage storage,
    ICurrentUserService currentUser,
    ILogger<EmployeeDocumentService> logger) : IEmployeeDocumentService
{
    private const string ConcurrencyConflict =
        "Conflitto: il documento dipendente è stato modificato. Ricaricare i dati e riprovare.";

    public async Task<PagedResult<EmployeeDocumentDto>> ListAsync(
        Guid employeeId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (!currentUser.HasRole("Admin"))
            return new([], 1, 20, 0);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.EmployeeDocuments.AsNoTracking()
            .Where(document => document.EmployeeId == employeeId);
        var total = await query.CountAsync(cancellationToken);
        var documents = await query
            .OrderByDescending(document => document.CreatedAt)
            .ThenBy(document => document.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new(documents.Select(Map).ToList(), page, pageSize, total);
    }

    public async Task<OperationResult<EmployeeDocumentDto>> CreateAsync(
        Guid employeeId,
        SaveEmployeeDocumentRequest request,
        Stream attachment,
        string fileName,
        string contentType,
        long length,
        string? userId,
        CancellationToken cancellationToken)
    {
        if (!currentUser.HasRole("Admin"))
            return OperationResult<EmployeeDocumentDto>.Fail("Solo un amministratore può gestire i documenti dipendente.");
        if (string.IsNullOrWhiteSpace(userId))
            return OperationResult<EmployeeDocumentDto>.Fail("Utente amministratore non identificato.");
        if (!await db.Employees.AnyAsync(employee => employee.Id == employeeId, cancellationToken))
            return OperationResult<EmployeeDocumentDto>.Fail("Dipendente non trovato.");
        if (!TryParseKind(request.Kind, out var kind))
            return OperationResult<EmployeeDocumentDto>.Fail("Tipo documento dipendente non valido.");

        var document = new EmployeeDocument(employeeId, userId);
        var details = document.SetDetails(
            kind, request.Title, request.DocumentNumber, request.IssuerOrProvider,
            request.IssuedOn, request.CompletedOn, request.ExpiresOn, request.Notes);
        if (!details.Success)
            return OperationResult<EmployeeDocumentDto>.Fail(details.Error!);

        var attachmentBuffer = await DocumentAttachmentValidator.ReadAndValidateAsync(
            attachment, fileName, contentType, length, cancellationToken);
        if (!attachmentBuffer.Success)
            return OperationResult<EmployeeDocumentDto>.Fail(attachmentBuffer.Error!);
        using var buffer = attachmentBuffer.Data!;

        buffer.Position = 0;
        var storageKey = await storage.UploadAsync(
            employeeId, document.Id, buffer, contentType, cancellationToken);
        var attached = document.SetAttachment(
            storageKey, Path.GetFileName(fileName), contentType, buffer.Length);
        if (!attached.Success)
        {
            await storage.DeleteAsync(storageKey, CancellationToken.None);
            return OperationResult<EmployeeDocumentDto>.Fail(attached.Error!);
        }

        db.EmployeeDocuments.Add(document);
        db.DocumentAuditEntries.Add(new DocumentAuditEntry(
            AuditDocumentTypes.EmployeeDocument,
            document.Id,
            AuditActions.EmployeeDocumentCreated,
            "Documento personale dipendente archiviato.",
            userId,
            currentUser.FullName ?? currentUser.Email ?? userId));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return OperationResult<EmployeeDocumentDto>.Ok(Map(document));
        }
        catch (DbUpdateConcurrencyException exception)
        {
            db.ChangeTracker.Clear();
            await DeleteUploadedAsync(storageKey, exception);
            logger.LogWarning(exception, "Concurrent employee document creation rejected for {EmployeeDocumentId}", document.Id);
            return OperationResult<EmployeeDocumentDto>.Fail(ConcurrencyConflict);
        }
        catch (Exception exception)
        {
            db.ChangeTracker.Clear();
            await DeleteUploadedAsync(storageKey, exception);
            throw;
        }
    }

    public async Task<OperationResult<EmployeeDocumentDto>> UpdateAsync(
        Guid employeeId,
        Guid documentId,
        SaveEmployeeDocumentRequest request,
        string? userId,
        CancellationToken cancellationToken)
    {
        if (!currentUser.HasRole("Admin"))
            return OperationResult<EmployeeDocumentDto>.Fail("Solo un amministratore può gestire i documenti dipendente.");
        if (string.IsNullOrWhiteSpace(userId))
            return OperationResult<EmployeeDocumentDto>.Fail("Utente amministratore non identificato.");

        var document = await db.EmployeeDocuments.SingleOrDefaultAsync(
            item => item.Id == documentId && item.EmployeeId == employeeId, cancellationToken);
        if (document is null)
            return OperationResult<EmployeeDocumentDto>.Fail("Documento dipendente non trovato.");
        if (!request.Version.HasValue || request.Version.Value != document.Version)
            return OperationResult<EmployeeDocumentDto>.Fail(ConcurrencyConflict);
        if (!TryParseKind(request.Kind, out var kind))
            return OperationResult<EmployeeDocumentDto>.Fail("Tipo documento dipendente non valido.");

        var updated = document.SetDetails(
            kind, request.Title, request.DocumentNumber, request.IssuerOrProvider,
            request.IssuedOn, request.CompletedOn, request.ExpiresOn, request.Notes);
        if (!updated.Success)
            return OperationResult<EmployeeDocumentDto>.Fail(updated.Error!);

        db.DocumentAuditEntries.Add(new DocumentAuditEntry(
            AuditDocumentTypes.EmployeeDocument,
            document.Id,
            AuditActions.EmployeeDocumentUpdated,
            "Metadati del documento dipendente aggiornati.",
            userId,
            currentUser.FullName ?? currentUser.Email ?? userId));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return OperationResult<EmployeeDocumentDto>.Ok(Map(document));
        }
        catch (DbUpdateConcurrencyException exception)
        {
            db.ChangeTracker.Clear();
            logger.LogWarning(exception, "Concurrent employee document update rejected for {EmployeeDocumentId}", documentId);
            return OperationResult<EmployeeDocumentDto>.Fail(ConcurrencyConflict);
        }
    }

    public async Task<(Stream Stream, string ContentType, string FileName)?> DownloadAsync(
        Guid employeeId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        if (!currentUser.HasRole("Admin"))
            return null;
        var document = await db.EmployeeDocuments.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == documentId && item.EmployeeId == employeeId,
                cancellationToken);
        if (document is null)
            return null;

        var file = await storage.DownloadAsync(document.StorageKey, cancellationToken);
        return (file.Stream, file.ContentType, document.FileName);
    }

    private async Task DeleteUploadedAsync(string storageKey, Exception originalException)
    {
        try
        {
            await storage.DeleteAsync(storageKey, CancellationToken.None);
        }
        catch (Exception cleanupException)
        {
            throw new AggregateException(
                "Salvataggio documento dipendente fallito e pulizia S3 non riuscita.",
                originalException,
                cleanupException);
        }
    }

    private static bool TryParseKind(string? value, out EmployeeDocumentKind kind) =>
        Enum.TryParse(value, true, out kind) && Enum.IsDefined(kind);

    private static EmployeeDocumentDto Map(EmployeeDocument document) => new(
        document.Id,
        document.EmployeeId,
        document.Kind.ToString(),
        document.Title,
        document.DocumentNumber,
        document.IssuerOrProvider,
        document.IssuedOn,
        document.CompletedOn,
        document.ExpiresOn,
        document.Notes,
        document.FileName,
        document.ContentType,
        document.FileLength,
        document.CreatedAt,
        document.Version);
}
