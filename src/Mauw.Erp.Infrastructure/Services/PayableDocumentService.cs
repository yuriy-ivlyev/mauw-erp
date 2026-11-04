using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Accounting;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Services;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Mauw.Erp.Infrastructure.Services;

public sealed class PayableDocumentService(
    MauwDbContext db,
    IPayableDocumentStorage storage,
    ItalianAccountingCalculator accountingCalculator,
    ICurrentUserService currentUser,
    ILogger<PayableDocumentService> logger) : IPayableDocumentService
{
    public const long MaximumFileSize = 20 * 1024 * 1024;
    private const string Missing = "Documento passivo non trovato.";
    private const string ConcurrencyConflict =
        "Conflitto: il documento è stato modificato. Ricaricare i dati e riprovare.";
    private const string DatabaseConcurrencyConflict =
        "Conflitto: il documento è stato aggiornato in concorrenza durante il salvataggio. Ricaricare i dati.";

    private IQueryable<PayableDocument> Documents => db.PayableDocuments
        .Include(document => document.Supplier)
        .Include(document => document.Employee)
        .Include(document => document.Lines)
        .Include(document => document.Payments);

    public async Task<PagedResult<PayableDocumentListItemDto>> ListAsync(
        int page,
        int pageSize,
        string? search,
        string? kind,
        string? status,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.PayableDocuments.AsQueryable();
        if (!currentUser.HasRole("Admin"))
            query = query.Where(document =>
                document.Kind != PayableDocumentKind.ExpenseReimbursement
                && document.Kind != PayableDocumentKind.EmployeePayroll);
        if (Enum.TryParse<PayableDocumentKind>(kind, true, out var documentKind)
            && Enum.IsDefined(documentKind))
            query = query.Where(document => document.Kind == documentKind);
        if (Enum.TryParse<PayableDocumentStatus>(status, true, out var documentStatus)
            && Enum.IsDefined(documentStatus))
            query = query.Where(document => document.Status == documentStatus);
        if (dateFrom.HasValue)
            query = query.Where(document => document.DocumentDate >= dateFrom.Value);
        if (dateTo.HasValue)
            query = query.Where(document => document.DocumentDate <= dateTo.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(document =>
                document.DocumentCode.ToLower().Contains(term)
                || (document.SourceDocumentNumber != null
                    && document.SourceDocumentNumber.ToLower().Contains(term))
                || (document.Supplier != null && document.Supplier.BusinessName.ToLower().Contains(term))
                || (document.BeneficiaryName != null && document.BeneficiaryName.ToLower().Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var documents = await query
            .OrderByDescending(document => document.DocumentDate)
            .ThenByDescending(document => document.CreatedAt)
            .ThenBy(document => document.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(document => document.Supplier)
            .Include(document => document.Employee)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return new(documents.Select(MapListItem).ToList(), page, pageSize, total);
    }

    public async Task<PagedResult<PayableDocumentListItemDto>> ListReimbursementsAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (!currentUser.HasRole("Admin"))
            return new([], 1, 20, 0);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.PayableDocuments
            .Where(document => document.Kind == PayableDocumentKind.ExpenseReimbursement);
        var total = await query.CountAsync(cancellationToken);
        var documents = await query
            .OrderByDescending(document => document.DocumentDate)
            .ThenByDescending(document => document.CreatedAt)
            .ThenBy(document => document.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(document => document.Supplier)
            .Include(document => document.Employee)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return new(documents.Select(MapListItem).ToList(), page, pageSize, total);
    }

    public async Task<PagedResult<PayableDocumentListItemDto>> ListEmployeePayrollCostsAsync(
        Guid employeeId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (!currentUser.HasRole("Admin"))
            return new([], 1, 20, 0);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.PayableDocuments.Where(document =>
            document.Kind == PayableDocumentKind.EmployeePayroll
            && document.EmployeeId == employeeId);
        var total = await query.CountAsync(cancellationToken);
        var documents = await query
            .OrderByDescending(document => document.DocumentDate)
            .ThenByDescending(document => document.CreatedAt)
            .ThenBy(document => document.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(document => document.Employee)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return new(documents.Select(MapListItem).ToList(), page, pageSize, total);
    }

    public async Task<PayableDocumentDto?> GetAsync(
        Guid id,
        string? requestingUserId,
        bool canManageAll,
        CancellationToken cancellationToken)
    {
        var document = await Documents.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (document is null || !CanAccess(document, requestingUserId, canManageAll))
            return null;
        return Map(document);
    }

    public Task<OperationResult<PayableDocumentDto>> CreateSupplierInvoiceAsync(
        CreatePayableDocumentRequest request,
        string? createdByUserId,
        CancellationToken cancellationToken) =>
        CreateAsync(request, PayableDocumentKind.SupplierInvoice, createdByUserId, cancellationToken);

    public async Task<OperationResult<PayableDocumentDto>> CreateReimbursementAsync(
        CreatePayableDocumentRequest request,
        string? createdByUserId,
        CancellationToken cancellationToken)
    {
        if (!currentUser.HasRole("Admin"))
            return OperationResult<PayableDocumentDto>.Fail("Solo un amministratore può registrare un rimborso.");
        if (string.IsNullOrWhiteSpace(createdByUserId))
            return OperationResult<PayableDocumentDto>.Fail("Utente amministratore non identificato.");
        if (request.SupplierId.HasValue)
            return OperationResult<PayableDocumentDto>.Fail("Un rimborso non può avere un fornitore.");
        var employee = request.EmployeeId.HasValue
            ? await db.Employees.SingleOrDefaultAsync(
                item => item.Id == request.EmployeeId.Value, cancellationToken)
            : null;
        if (employee is null || !employee.IsActive)
            return OperationResult<PayableDocumentDto>.Fail("Selezionare un dipendente attivo.");

        return await CreateAsync(request with
        {
            Kind = PayableDocumentKind.ExpenseReimbursement.ToString(),
            BeneficiaryName = $"{employee.FirstName} {employee.LastName}"
        }, PayableDocumentKind.ExpenseReimbursement, createdByUserId, cancellationToken);
    }

    public async Task<OperationResult<PayableDocumentDto>> CreateEmployeePayrollCostAsync(
        Guid employeeId,
        CreateEmployeePayrollCostRequest request,
        Stream attachment,
        string fileName,
        string contentType,
        long length,
        string? createdByUserId,
        CancellationToken cancellationToken)
    {
        if (!currentUser.HasRole("Admin"))
            return OperationResult<PayableDocumentDto>.Fail("Solo un amministratore può registrare costi del personale.");
        if (string.IsNullOrWhiteSpace(createdByUserId))
            return OperationResult<PayableDocumentDto>.Fail("Utente amministratore non identificato.");
        if (request.Period == default || request.Period.Day != 1
            || request.MonthlyCompanyCost <= 0m
            || decimal.Round(request.MonthlyCompanyCost, 2, MidpointRounding.AwayFromZero)
                != request.MonthlyCompanyCost)
            return OperationResult<PayableDocumentDto>.Fail(
                "Indicare il primo giorno del mese e un costo aziendale positivo a due decimali.");
        var attachmentBuffer = await ReadAndValidateAttachmentAsync(
            attachment, fileName, contentType, length, cancellationToken);
        if (!attachmentBuffer.Success)
            return OperationResult<PayableDocumentDto>.Fail(attachmentBuffer.Error!);
        using var buffer = attachmentBuffer.Data!;

        var employee = await db.Employees.SingleOrDefaultAsync(
            item => item.Id == employeeId, cancellationToken);
        if (employee is null)
            return OperationResult<PayableDocumentDto>.Fail("Dipendente non trovato.");
        var periodEnd = request.Period.AddMonths(1).AddDays(-1);
        if (employee.HireDate > periodEnd || employee.EndDate < request.Period)
            return OperationResult<PayableDocumentDto>.Fail(
                "Il periodo del costo non ricade nel periodo di impiego del dipendente.");

        var document = new PayableDocument(PayableDocumentKind.EmployeePayroll, createdByUserId);
        var line = new PayableDocumentLine(
            document.Id,
            $"Costo aziendale mensile - {employee.EmployeeNumber} {request.Period:MM/yyyy}",
            PayableExpenseCategory.Payroll,
            request.MonthlyCompanyCost,
            null,
            null,
            0.00m,
            request.MonthlyCompanyCost,
            null,
            null);
        var updated = document.UpdateDraft(
            null,
            request.Period.ToString("yyyy-MM"),
            request.Period,
            periodEnd,
            $"{employee.FirstName} {employee.LastName}",
            request.Notes,
            [line],
            employeeId);
        if (!updated.Success)
            return OperationResult<PayableDocumentDto>.Fail(updated.Error!);

        buffer.Position = 0;
        var storageKey = await storage.UploadAsync(document.Id, buffer, contentType, cancellationToken);
        var attached = document.SetAttachment(
            storageKey, Path.GetFileName(fileName), contentType, buffer.Length);
        if (!attached.Success)
        {
            await storage.DeleteAsync(storageKey, CancellationToken.None);
            return OperationResult<PayableDocumentDto>.Fail(attached.Error!);
        }
        var submitted = document.SubmitForApproval(DateTime.UtcNow);
        if (!submitted.Success)
        {
            await storage.DeleteAsync(storageKey, CancellationToken.None);
            return OperationResult<PayableDocumentDto>.Fail(submitted.Error!);
        }
        var approved = document.Approve(createdByUserId, DateTime.UtcNow);
        if (!approved.Success)
        {
            await storage.DeleteAsync(storageKey, CancellationToken.None);
            return OperationResult<PayableDocumentDto>.Fail(approved.Error!);
        }

        db.PayableDocuments.Add(document);
        db.DocumentAuditEntries.Add(new DocumentAuditEntry(
            AuditDocumentTypes.PayableDocument,
            document.Id,
            AuditActions.EmployeePayrollCostCreated,
            "Costo aziendale mensile e cedolino registrati.",
            createdByUserId,
            currentUser.FullName ?? currentUser.Email ?? createdByUserId));
        db.DocumentAuditEntries.Add(new DocumentAuditEntry(
            AuditDocumentTypes.PayableDocument,
            document.Id,
            AuditActions.PayableDocumentApproved,
            "Costo mensile verificato e registrato come spesa passiva.",
            createdByUserId,
            currentUser.FullName ?? currentUser.Email ?? createdByUserId,
            occurredAtUtc: DateTime.UtcNow));
        PayableDocumentOperationResult saved;
        try
        {
            saved = await SaveAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            try
            {
                await storage.DeleteAsync(storageKey, CancellationToken.None);
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException(
                    "Salvataggio costo dipendente fallito e pulizia S3 non riuscita.",
                    exception,
                    cleanupException);
            }
            throw;
        }

        if (!saved.Success)
        {
            await storage.DeleteAsync(storageKey, CancellationToken.None);
            return OperationResult<PayableDocumentDto>.Fail(saved.Error!);
        }
        var created = await GetAsync(document.Id, createdByUserId, canManageAll: true, cancellationToken);
        return created is null
            ? OperationResult<PayableDocumentDto>.Fail("Costo dipendente non trovato dopo il salvataggio.")
            : OperationResult<PayableDocumentDto>.Ok(created);
    }

    public async Task<OperationResult<PayableDocumentDto>> UpdateDraftAsync(
        Guid id,
        CreatePayableDocumentRequest request,
        string? requestingUserId,
        bool canManageAll,
        CancellationToken cancellationToken)
    {
        var document = await Documents.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (document is null)
            return OperationResult<PayableDocumentDto>.Fail(Missing);
        if (!CanAccess(document, requestingUserId, canManageAll))
            return OperationResult<PayableDocumentDto>.Fail("Non si dispone dei permessi per modificare questo documento.");
        if (!request.Version.HasValue || request.Version.Value != document.Version)
            return OperationResult<PayableDocumentDto>.Fail(ConcurrencyConflict);
        if (!KindMatches(document.Kind, request.Kind))
            return OperationResult<PayableDocumentDto>.Fail("Il tipo del documento non può essere modificato.");

        var supplierError = await ValidateSupplierAsync(document.Kind, request.SupplierId, cancellationToken);
        if (supplierError is not null)
            return OperationResult<PayableDocumentDto>.Fail(supplierError);
        var employeeValidation = await ValidateEmployeeAsync(
            document.Kind, request.EmployeeId, request.DocumentDate, request.DueDate, cancellationToken);
        if (employeeValidation.Error is not null)
            return OperationResult<PayableDocumentDto>.Fail(employeeValidation.Error);

        var lines = BuildLines(document.Id, document.Kind, request.Lines);
        if (!lines.Success)
            return OperationResult<PayableDocumentDto>.Fail(lines.Error!);
        var beneficiaryName = employeeValidation.Employee is { } employee
            ? $"{employee.FirstName} {employee.LastName}"
            : request.BeneficiaryName;
        var updated = document.UpdateDraft(
            request.SupplierId,
            request.SourceDocumentNumber,
            request.DocumentDate,
            request.DueDate,
            beneficiaryName,
            request.Notes,
            lines.Data!,
            request.EmployeeId);
        if (!updated.Success)
            return OperationResult<PayableDocumentDto>.Fail(updated.Error!);

        db.DocumentAuditEntries.Add(new DocumentAuditEntry(
            AuditDocumentTypes.PayableDocument,
            document.Id,
            AuditActions.PayableDocumentUpdated,
            "Bozza documento passivo aggiornata.",
            requestingUserId,
            currentUser.FullName ?? currentUser.Email ?? requestingUserId));
        var saved = await SaveAsync(cancellationToken);
        if (!saved.Success)
            return OperationResult<PayableDocumentDto>.Fail(saved.Error!);
        return OperationResult<PayableDocumentDto>.Ok(Map(document));
    }

    public async Task<OperationResult<PayableDocumentDto>> UploadAttachmentAsync(
        Guid id,
        Guid version,
        Stream stream,
        string fileName,
        string contentType,
        long length,
        string? requestingUserId,
        bool canManageAll,
        CancellationToken cancellationToken)
    {
        var attachmentBuffer = await ReadAndValidateAttachmentAsync(
            stream, fileName, contentType, length, cancellationToken);
        if (!attachmentBuffer.Success)
            return OperationResult<PayableDocumentDto>.Fail(attachmentBuffer.Error!);
        using var buffer = attachmentBuffer.Data!;

        var document = await Documents.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (document is null)
            return OperationResult<PayableDocumentDto>.Fail(Missing);
        if (!CanAccess(document, requestingUserId, canManageAll))
            return OperationResult<PayableDocumentDto>.Fail("Non si dispone dei permessi per modificare questo documento.");
        if (document.Version != version)
            return OperationResult<PayableDocumentDto>.Fail(ConcurrencyConflict);

        var previousKey = document.AttachmentKey;
        buffer.Position = 0;
        var storageKey = await storage.UploadAsync(document.Id, buffer, contentType, cancellationToken);
        var updated = document.SetAttachment(
            storageKey, Path.GetFileName(fileName), contentType, buffer.Length);
        if (!updated.Success)
        {
            await storage.DeleteAsync(storageKey, CancellationToken.None);
            return OperationResult<PayableDocumentDto>.Fail(updated.Error!);
        }
        db.DocumentAuditEntries.Add(new DocumentAuditEntry(
            AuditDocumentTypes.PayableDocument,
            document.Id,
            AuditActions.PayableDocumentAttachmentUploaded,
            "Allegato del documento passivo caricato.",
            requestingUserId,
            currentUser.FullName ?? currentUser.Email ?? requestingUserId,
            Path.GetFileName(fileName)));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            db.ChangeTracker.Clear();
            logger.LogWarning(exception, "Concurrent payable attachment update rejected for {PayableDocumentId}", id);
            await storage.DeleteAsync(storageKey, CancellationToken.None);
            return OperationResult<PayableDocumentDto>.Fail(ConcurrencyConflict);
        }
        catch (Exception exception)
        {
            try
            {
                await storage.DeleteAsync(storageKey, CancellationToken.None);
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException(
                    "Salvataggio allegato fallito e pulizia S3 non riuscita.",
                    exception,
                    cleanupException);
            }
            throw;
        }

        if (previousKey is not null && previousKey != storageKey)
            await storage.DeleteAsync(previousKey, cancellationToken);
        return OperationResult<PayableDocumentDto>.Ok(Map(document));
    }

    public async Task<(Stream Stream, string ContentType, string FileName)?> DownloadAttachmentAsync(
        Guid id,
        string? requestingUserId,
        bool canManageAll,
        CancellationToken cancellationToken)
    {
        var document = await db.PayableDocuments.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (document is null || !CanAccess(document, requestingUserId, canManageAll)
            || string.IsNullOrWhiteSpace(document.AttachmentKey)
            || string.IsNullOrWhiteSpace(document.AttachmentFileName)
            || string.IsNullOrWhiteSpace(document.AttachmentContentType))
            return null;

        var file = await storage.DownloadAsync(document.AttachmentKey, cancellationToken);
        return (file.Stream, file.ContentType, document.AttachmentFileName);
    }

    public Task<OperationResult<PayableDocumentDto>> SubmitAsync(
        Guid id,
        Guid version,
        string? requestingUserId,
        bool canManageAll,
        CancellationToken cancellationToken) =>
        UpdateWorkflowAsync(
            id, version, requestingUserId, canManageAll,
            document => document.SubmitForApproval(DateTime.UtcNow),
            AuditActions.PayableDocumentSubmitted,
            "Documento passivo inviato in approvazione.",
            cancellationToken);

    public Task<OperationResult<PayableDocumentDto>> ApproveAsync(
        Guid id,
        Guid version,
        string? approvedByUserId,
        CancellationToken cancellationToken) =>
        UpdateWorkflowAsync(
            id, version, approvedByUserId, canManageAll: true,
            document => document.Approve(approvedByUserId, DateTime.UtcNow),
            AuditActions.PayableDocumentApproved,
            "Documento passivo approvato.",
            cancellationToken);

    public Task<OperationResult<PayableDocumentDto>> RejectAsync(
        Guid id,
        Guid version,
        string? rejectedByUserId,
        string reason,
        CancellationToken cancellationToken) =>
        UpdateWorkflowAsync(
            id, version, rejectedByUserId, canManageAll: true,
            document => document.Reject(reason),
            AuditActions.PayableDocumentRejected,
            "Documento passivo respinto.",
            cancellationToken);

    public Task<OperationResult<PayableDocumentDto>> CancelAsync(
        Guid id,
        Guid version,
        string? requestingUserId,
        bool canManageAll,
        CancellationToken cancellationToken) =>
        UpdateWorkflowAsync(
            id, version, requestingUserId, canManageAll,
            document => document.Cancel(),
            AuditActions.PayableDocumentCancelled,
            "Documento passivo annullato.",
            cancellationToken);

    public async Task<OperationResult<PayableDocumentDto>> RecordPaymentAsync(
        Guid id,
        PayableDocumentPaymentRequest request,
        string? recordedByUserId,
        CancellationToken cancellationToken)
    {
        var document = await Documents.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (document is null)
            return OperationResult<PayableDocumentDto>.Fail(Missing);
        if (!CanAccess(document, recordedByUserId, canManageAll: true))
            return OperationResult<PayableDocumentDto>.Fail("Non si dispone dei permessi per modificare questo documento.");
        if (document.Version != request.Version)
            return OperationResult<PayableDocumentDto>.Fail(ConcurrencyConflict);

        var paymentDate = request.PaymentDate ?? DateTime.UtcNow;
        var payment = document.RecordPayment(
            request.Amount, paymentDate, request.PaymentMethod, request.TransactionDetails, recordedByUserId);
        if (!payment.Success)
            return OperationResult<PayableDocumentDto>.Fail(payment.Error!);

        db.DocumentAuditEntries.Add(new DocumentAuditEntry(
            AuditDocumentTypes.PayableDocument,
            document.Id,
            AuditActions.PayableDocumentPaymentRecorded,
            "Pagamento documento passivo registrato.",
            recordedByUserId,
            currentUser.FullName ?? currentUser.Email ?? recordedByUserId,
            details: $"Importo {request.Amount:0.00}; metodo {request.PaymentMethod.Trim()}"));
        var saved = await SaveAsync(cancellationToken);
        return saved.Success
            ? OperationResult<PayableDocumentDto>.Ok(Map(document))
            : OperationResult<PayableDocumentDto>.Fail(saved.Error!);
    }

    private async Task<OperationResult<PayableDocumentDto>> CreateAsync(
        CreatePayableDocumentRequest request,
        PayableDocumentKind kind,
        string? userId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return OperationResult<PayableDocumentDto>.Fail("Utente autenticato non identificato.");
        if (!KindMatches(kind, request.Kind))
            return OperationResult<PayableDocumentDto>.Fail("Tipo documento non valido per questa operazione.");

        var supplierError = await ValidateSupplierAsync(kind, request.SupplierId, cancellationToken);
        if (supplierError is not null)
            return OperationResult<PayableDocumentDto>.Fail(supplierError);
        var employeeValidation = await ValidateEmployeeAsync(
            kind, request.EmployeeId, request.DocumentDate, request.DueDate, cancellationToken);
        if (employeeValidation.Error is not null)
            return OperationResult<PayableDocumentDto>.Fail(employeeValidation.Error);

        var document = new PayableDocument(kind, userId);
        var lines = BuildLines(document.Id, kind, request.Lines);
        if (!lines.Success)
            return OperationResult<PayableDocumentDto>.Fail(lines.Error!);
        var updated = document.UpdateDraft(
            request.SupplierId,
            request.SourceDocumentNumber,
            request.DocumentDate,
            request.DueDate,
            employeeValidation.Employee is { } employee
                ? $"{employee.FirstName} {employee.LastName}"
                : request.BeneficiaryName,
            request.Notes,
            lines.Data!,
            request.EmployeeId);
        if (!updated.Success)
            return OperationResult<PayableDocumentDto>.Fail(updated.Error!);

        db.PayableDocuments.Add(document);
        db.DocumentAuditEntries.Add(new DocumentAuditEntry(
            AuditDocumentTypes.PayableDocument,
            document.Id,
            AuditActions.PayableDocumentCreated,
            "Documento passivo creato in bozza.",
            userId,
            currentUser.FullName ?? currentUser.Email ?? userId));
        var saved = await SaveAsync(cancellationToken);
        if (!saved.Success)
            return OperationResult<PayableDocumentDto>.Fail(saved.Error!);
        var created = await GetAsync(document.Id, userId, canManageAll: true, cancellationToken);
        return created is null
            ? OperationResult<PayableDocumentDto>.Fail("Documento passivo non trovato dopo il salvataggio.")
            : OperationResult<PayableDocumentDto>.Ok(created);
    }

    private async Task<OperationResult<PayableDocumentDto>> UpdateWorkflowAsync(
        Guid id,
        Guid version,
        string? userId,
        bool canManageAll,
        Func<PayableDocument, PayableDocumentOperationResult> operation,
        string auditAction,
        string auditNote,
        CancellationToken cancellationToken)
    {
        var document = await Documents.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (document is null)
            return OperationResult<PayableDocumentDto>.Fail(Missing);
        if (!CanAccess(document, userId, canManageAll))
            return OperationResult<PayableDocumentDto>.Fail("Non si dispone dei permessi per modificare questo documento.");
        if (document.Version != version)
            return OperationResult<PayableDocumentDto>.Fail(ConcurrencyConflict);

        var changed = operation(document);
        if (!changed.Success)
            return OperationResult<PayableDocumentDto>.Fail(changed.Error!);

        db.DocumentAuditEntries.Add(new DocumentAuditEntry(
            AuditDocumentTypes.PayableDocument,
            document.Id,
            auditAction,
            auditNote,
            userId,
            currentUser.FullName ?? currentUser.Email ?? userId,
            occurredAtUtc: DateTime.UtcNow));
        var saved = await SaveAsync(cancellationToken);
        return saved.Success
            ? OperationResult<PayableDocumentDto>.Ok(Map(document))
            : OperationResult<PayableDocumentDto>.Fail(saved.Error!);
    }

    private async Task<PayableDocumentOperationResult> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return PayableDocumentOperationResult.Ok();
        }
        catch (DbUpdateConcurrencyException exception)
        {
            var entityTypes = string.Join(",",
                exception.Entries.Select(entry => entry.Metadata.ClrType.Name).Distinct());
            db.ChangeTracker.Clear();
            logger.LogWarning(exception, "Concurrent payable document update rejected for {EntityTypes}", entityTypes);
            return PayableDocumentOperationResult.Fail(DatabaseConcurrencyConflict);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation
            && postgres.ConstraintName?.Contains(
                "payable_documents_supplier_id_source_document_number",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            db.ChangeTracker.Clear();
            return PayableDocumentOperationResult.Fail("La fattura con questo numero è già registrata per il fornitore.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation
            && postgres.ConstraintName?.Contains(
                "employee_id_source_document_number",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            db.ChangeTracker.Clear();
            return PayableDocumentOperationResult.Fail(
                "Il costo del dipendente per questo mese è già registrato.");
        }
    }

    private async Task<string?> ValidateSupplierAsync(
        PayableDocumentKind kind,
        Guid? supplierId,
        CancellationToken cancellationToken)
    {
        if (kind != PayableDocumentKind.SupplierInvoice)
            return supplierId.HasValue
                ? "Questo documento non può avere un fornitore."
                : null;
        if (!supplierId.HasValue || supplierId == Guid.Empty)
            return "Selezionare il fornitore.";

        var exists = await db.Contacts.AnyAsync(contact =>
            contact.Id == supplierId.Value && !contact.IsDeleted
            && contact.Status == ContactStatus.Active && contact.SupplierProfile != null,
            cancellationToken);
        return exists ? null : "Fornitore non valido o non attivo.";
    }

    private async Task<(Employee? Employee, string? Error)> ValidateEmployeeAsync(
        PayableDocumentKind kind,
        Guid? employeeId,
        DateOnly documentDate,
        DateOnly dueDate,
        CancellationToken cancellationToken)
    {
        if (kind == PayableDocumentKind.SupplierInvoice)
            return employeeId.HasValue
                ? (null, "Una fattura fornitore non può essere associata a un dipendente.")
                : (null, null);
        if (kind is not (PayableDocumentKind.ExpenseReimbursement or PayableDocumentKind.EmployeePayroll))
            return (null, "Tipo documento non supportato.");
        if (!employeeId.HasValue || employeeId == Guid.Empty)
            return (null, kind == PayableDocumentKind.ExpenseReimbursement
                ? "Selezionare il dipendente beneficiario del rimborso."
                : "Selezionare il dipendente.");

        var employee = await db.Employees.SingleOrDefaultAsync(
            item => item.Id == employeeId.Value, cancellationToken);
        if (employee is null)
            return (null, "Dipendente non trovato.");
        if (kind == PayableDocumentKind.EmployeePayroll
            && (employee.HireDate > dueDate || employee.EndDate < documentDate))
            return (null, "Il periodo del costo non ricade nel periodo di impiego del dipendente.");
        return (employee, null);
    }

    private OperationResult<List<PayableDocumentLine>> BuildLines(
        Guid documentId,
        PayableDocumentKind kind,
        IReadOnlyList<PayableDocumentLineRequest>? requests)
    {
        if (requests is null || requests.Count is 0 or > 200 || requests.Any(line => line is null))
            return OperationResult<List<PayableDocumentLine>>.Fail("Indicare da 1 a 200 righe di spesa.");

        var lines = new List<PayableDocumentLine>(requests.Count);
        foreach (var request in requests)
        {
            if (!Enum.TryParse<PayableExpenseCategory>(request.Category, true, out var category)
                || !Enum.IsDefined(category))
                return OperationResult<List<PayableDocumentLine>>.Fail("Categoria di spesa non valida.");
            if (category == PayableExpenseCategory.Payroll
                && kind != PayableDocumentKind.EmployeePayroll)
                return OperationResult<List<PayableDocumentLine>>.Fail(
                    "La categoria paghe è riservata ai costi mensili dei dipendenti.");
            if (kind == PayableDocumentKind.EmployeePayroll
                && (requests.Count != 1 || category != PayableExpenseCategory.Payroll
                    || request.VatRate.HasValue || request.NatureCode is not null
                    || request.VatAmount != 0m
                    || request.TaxableAmount != request.GrossAmount))
                return OperationResult<List<PayableDocumentLine>>.Fail(
                    "Il costo mensile deve avere una sola riga senza IVA, di categoria paghe.");
            if (kind == PayableDocumentKind.SupplierInvoice && !request.VatRate.HasValue)
                return OperationResult<List<PayableDocumentLine>>.Fail(
                    "Indicare aliquota e Natura IVA dalla fattura del fornitore.");
            if (request.VatRate.HasValue)
            {
                var fiscalLine = accountingCalculator.CalculateLine(new DocumentLineInput(
                    1m,
                    request.TaxableAmount,
                    request.VatRate.Value,
                    request.NatureCode));
                if (!fiscalLine.Success)
                    return OperationResult<List<PayableDocumentLine>>.Fail(
                        $"Classificazione IVA non valida: {fiscalLine.Error}");
            }

            lines.Add(new PayableDocumentLine(
                documentId,
                request.Description,
                category,
                request.TaxableAmount,
                request.VatRate,
                request.NatureCode,
                request.VatAmount,
                request.GrossAmount,
                request.CostCenter,
                request.Project));
        }

        return OperationResult<List<PayableDocumentLine>>.Ok(lines);
    }

    private bool CanAccess(PayableDocument document, string? userId, bool canManageAll)
    {
        if (!canManageAll)
            return false;
        if (document.Kind is PayableDocumentKind.ExpenseReimbursement or PayableDocumentKind.EmployeePayroll)
            return currentUser.HasRole("Admin");
        return true;
    }

    private static bool KindMatches(PayableDocumentKind expected, string? requested) =>
        Enum.TryParse<PayableDocumentKind>(requested, true, out var parsed)
        && Enum.IsDefined(parsed)
        && parsed == expected;

    private static async Task<OperationResult<MemoryStream>> ReadAndValidateAttachmentAsync(
        Stream stream,
        string fileName,
        string contentType,
        long length,
        CancellationToken cancellationToken)
    {
        if (length <= 0 || length > MaximumFileSize)
            return OperationResult<MemoryStream>.Fail("Allegare un file non vuoto di massimo 20 MB.");
        if (!AllowedContentType(contentType))
            return OperationResult<MemoryStream>.Fail("Sono ammessi solo file PDF, PNG e JPEG.");
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if ((contentType, extension) is not (("application/pdf", ".pdf")
            or ("image/png", ".png") or ("image/jpeg", ".jpg") or ("image/jpeg", ".jpeg")))
            return OperationResult<MemoryStream>.Fail("Estensione e formato dell'allegato non corrispondono.");

        var buffer = new MemoryStream();
        try
        {
            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + read > MaximumFileSize)
                {
                    buffer.Dispose();
                    return OperationResult<MemoryStream>.Fail("Il documento supera 20 MB.");
                }
                await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
            }
            if (!ValidSignature(buffer.GetBuffer().AsSpan(0, (int)buffer.Length), contentType))
            {
                buffer.Dispose();
                return OperationResult<MemoryStream>.Fail(
                    "Il contenuto del file non corrisponde al formato dichiarato.");
            }
            buffer.Position = 0;
            return OperationResult<MemoryStream>.Ok(buffer);
        }
        catch
        {
            buffer.Dispose();
            throw;
        }
    }

    private static PayableDocumentListItemDto MapListItem(PayableDocument document) => new(
        document.Id,
        document.DocumentCode,
        document.Kind.ToString(),
        document.EmployeeId,
        document.Employee?.EmployeeNumber,
        document.Supplier?.BusinessName,
        document.SourceDocumentNumber,
        document.DocumentDate,
        document.DueDate,
        document.BeneficiaryName,
        document.Status.ToString(),
        document.PaymentStatus.ToString(),
        document.TaxableTotal,
        document.VatTotal,
        document.GrossTotal,
        document.PaidAmount,
        document.BalanceDue,
        document.AttachmentFileName,
        !string.IsNullOrWhiteSpace(document.AttachmentKey));

    private static PayableDocumentDto Map(PayableDocument document) => new(
        document.Id,
        document.DocumentCode,
        document.Kind.ToString(),
        document.SupplierId,
        document.Supplier?.BusinessName,
        document.EmployeeId,
        document.Employee?.EmployeeNumber,
        document.SourceDocumentNumber,
        document.DocumentDate,
        document.DueDate,
        document.BeneficiaryName,
        document.Notes,
        document.Status.ToString(),
        document.PaymentStatus.ToString(),
        document.TaxableTotal,
        document.VatTotal,
        document.GrossTotal,
        document.PaidAmount,
        document.BalanceDue,
        document.AttachmentFileName,
        document.AttachmentContentType,
        document.AttachmentLength,
        document.CreatedByUserId,
        document.ApprovedByUserId,
        document.SubmittedAtUtc,
        document.ApprovedAtUtc,
        document.RejectionReason,
        document.Version,
        document.Lines.OrderBy(line => line.CreatedAt)
            .Select(line => new PayableDocumentLineDto(
                line.Id,
                line.Description,
                line.Category.ToString(),
                line.CostCenter,
                line.Project,
                line.TaxableAmount,
                line.VatRate,
                line.NatureCode,
                line.VatAmount,
                line.GrossAmount))
            .ToList(),
        document.Payments.OrderBy(payment => payment.PaymentDateUtc)
            .Select(payment => new PayableDocumentPaymentDto(
                payment.Id,
                payment.Amount,
                payment.PaymentDateUtc,
                payment.PaymentMethod,
                payment.TransactionDetails,
                payment.CreatedByUserId))
            .ToList());

    private static bool AllowedContentType(string contentType) =>
        contentType is "application/pdf" or "image/png" or "image/jpeg";

    private static bool ValidSignature(ReadOnlySpan<byte> bytes, string contentType) =>
        contentType switch
        {
            "application/pdf" => bytes.StartsWith("%PDF-"u8),
            "image/png" => bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            _ => false
        };
}
