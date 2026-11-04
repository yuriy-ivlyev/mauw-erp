using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Mauw.Erp.Infrastructure.Services;

public sealed class EmployeeTimeEntryService(
    MauwDbContext db,
    ICurrentUserService currentUser,
    ILogger<EmployeeTimeEntryService> logger) : IEmployeeTimeEntryService
{
    private const string ConcurrencyConflict =
        "Conflitto: il cartellino è stato modificato. Ricaricare i dati e riprovare.";

    public async Task<PagedResult<EmployeeTimeEntryDto>> ListAsync(
        Guid employeeId,
        DateOnly from,
        DateOnly to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (!currentUser.HasRole("Admin"))
            return new([], 1, 20, 0);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.EmployeeTimeEntries.AsNoTracking()
            .Where(entry => entry.EmployeeId == employeeId
                && entry.WorkDate >= from && entry.WorkDate <= to);
        var total = await query.CountAsync(cancellationToken);
        var entries = await query
            .OrderByDescending(entry => entry.WorkDate)
            .ThenBy(entry => entry.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new(entries.Select(Map).ToList(), page, pageSize, total);
    }

    public async Task<OperationResult<EmployeeTimeEntryDto>> CreateAsync(
        Guid employeeId,
        SaveEmployeeTimeEntryRequest request,
        string? userId,
        CancellationToken cancellationToken)
    {
        if (!currentUser.HasRole("Admin"))
            return OperationResult<EmployeeTimeEntryDto>.Fail("Solo un amministratore può gestire i cartellini.");
        if (string.IsNullOrWhiteSpace(userId))
            return OperationResult<EmployeeTimeEntryDto>.Fail("Utente amministratore non identificato.");
        if (request.Version.HasValue)
            return OperationResult<EmployeeTimeEntryDto>.Fail(
                "La versione non deve essere indicata durante la creazione.");

        var employee = await db.Employees.SingleOrDefaultAsync(
            item => item.Id == employeeId, cancellationToken);
        if (employee is null)
            return OperationResult<EmployeeTimeEntryDto>.Fail("Dipendente non trovato.");
        var employmentError = ValidateEmployment(employee, request.WorkDate);
        if (employmentError is not null)
            return OperationResult<EmployeeTimeEntryDto>.Fail(employmentError);
        if (!TryParseType(request.Type, out var type))
            return OperationResult<EmployeeTimeEntryDto>.Fail("Tipo di cartellino non valido.");

        var entry = new EmployeeTimeEntry(employeeId, userId);
        var updated = entry.Update(
            request.WorkDate, type, request.StartTime, request.EndTime,
            request.BreakMinutes, request.Notes);
        if (!updated.Success)
            return OperationResult<EmployeeTimeEntryDto>.Fail(updated.Error!);
        db.EmployeeTimeEntries.Add(entry);
        db.DocumentAuditEntries.Add(new DocumentAuditEntry(
            AuditDocumentTypes.EmployeeTimeEntry,
            entry.Id,
            AuditActions.EmployeeTimeEntryCreated,
            "Cartellino giornaliero registrato.",
            userId,
            currentUser.FullName ?? currentUser.Email ?? userId));
        return await PersistAsync(entry, cancellationToken);
    }

    public async Task<OperationResult<EmployeeTimeEntryDto>> UpdateAsync(
        Guid employeeId,
        Guid entryId,
        SaveEmployeeTimeEntryRequest request,
        string? userId,
        CancellationToken cancellationToken)
    {
        if (!currentUser.HasRole("Admin"))
            return OperationResult<EmployeeTimeEntryDto>.Fail("Solo un amministratore può gestire i cartellini.");
        if (string.IsNullOrWhiteSpace(userId))
            return OperationResult<EmployeeTimeEntryDto>.Fail("Utente amministratore non identificato.");
        if (!request.Version.HasValue)
            return OperationResult<EmployeeTimeEntryDto>.Fail("La versione del cartellino è obbligatoria.");

        var entry = await db.EmployeeTimeEntries.SingleOrDefaultAsync(
            item => item.Id == entryId && item.EmployeeId == employeeId, cancellationToken);
        if (entry is null)
            return OperationResult<EmployeeTimeEntryDto>.Fail("Cartellino non trovato.");
        if (entry.Version != request.Version.Value)
            return OperationResult<EmployeeTimeEntryDto>.Fail(ConcurrencyConflict);

        var employee = await db.Employees.SingleAsync(
            item => item.Id == employeeId, cancellationToken);
        var employmentError = ValidateEmployment(employee, request.WorkDate);
        if (employmentError is not null)
            return OperationResult<EmployeeTimeEntryDto>.Fail(employmentError);
        if (!TryParseType(request.Type, out var type))
            return OperationResult<EmployeeTimeEntryDto>.Fail("Tipo di cartellino non valido.");

        var updated = entry.Update(
            request.WorkDate, type, request.StartTime, request.EndTime,
            request.BreakMinutes, request.Notes);
        if (!updated.Success)
            return OperationResult<EmployeeTimeEntryDto>.Fail(updated.Error!);
        db.DocumentAuditEntries.Add(new DocumentAuditEntry(
            AuditDocumentTypes.EmployeeTimeEntry,
            entry.Id,
            AuditActions.EmployeeTimeEntryUpdated,
            "Cartellino giornaliero aggiornato.",
            userId,
            currentUser.FullName ?? currentUser.Email ?? userId));
        return await PersistAsync(entry, cancellationToken);
    }

    private async Task<OperationResult<EmployeeTimeEntryDto>> PersistAsync(
        EmployeeTimeEntry entry,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return OperationResult<EmployeeTimeEntryDto>.Ok(Map(entry));
        }
        catch (DbUpdateConcurrencyException exception)
        {
            db.ChangeTracker.Clear();
            logger.LogWarning(exception, "Concurrent employee time entry update rejected for {EmployeeTimeEntryId}", entry.Id);
            return OperationResult<EmployeeTimeEntryDto>.Fail(ConcurrencyConflict);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation
            && postgres.ConstraintName?.Contains(
                "employee_id_work_date",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            db.ChangeTracker.Clear();
            return OperationResult<EmployeeTimeEntryDto>.Fail(
                "Esiste già un cartellino per il dipendente nella data indicata.");
        }
    }

    private static string? ValidateEmployment(Employee employee, DateOnly workDate)
    {
        if (employee.HireDate > workDate || employee.EndDate < workDate)
            return "La data del cartellino non ricade nel periodo di impiego del dipendente.";
        return null;
    }

    private static bool TryParseType(string? value, out EmployeeTimeEntryType type) =>
        Enum.TryParse(value, true, out type) && Enum.IsDefined(type);

    private static EmployeeTimeEntryDto Map(EmployeeTimeEntry entry) => new(
        entry.Id,
        entry.EmployeeId,
        entry.WorkDate,
        entry.Type.ToString(),
        entry.StartTime,
        entry.EndTime,
        entry.BreakMinutes,
        entry.WorkedMinutes,
        entry.Notes,
        entry.Version);
}
