using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Mauw.Erp.Infrastructure.Services;

public sealed class EmployeeService(
    MauwDbContext db,
    ICurrentUserService currentUser) : IEmployeeService
{
    private const string ConcurrencyConflict =
        "Conflitto: il dipendente è stato modificato. Ricaricare i dati e riprovare.";

    public async Task<PagedResult<EmployeeDto>> ListAsync(
        int page,
        int pageSize,
        string? search,
        bool activeOnly,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Employees.AsNoTracking().AsQueryable();
        if (activeOnly)
            query = query.Where(employee => employee.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(employee =>
                employee.EmployeeNumber.ToLower().Contains(term)
                || employee.FirstName.ToLower().Contains(term)
                || employee.LastName.ToLower().Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var employees = await query
            .OrderBy(employee => employee.EmployeeNumber)
            .ThenBy(employee => employee.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new(employees.Select(Map).ToList(), page, pageSize, total);
    }

    public async Task<EmployeeDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return employee is null ? null : Map(employee);
    }

    public Task<OperationResult<EmployeeDto>> CreateAsync(
        SaveEmployeeRequest request,
        string? userId,
        CancellationToken cancellationToken)
    {
        if (request.Version.HasValue)
            return Task.FromResult(OperationResult<EmployeeDto>.Fail(
                "La versione non deve essere indicata durante la creazione."));
        var employee = new Employee();
        return SaveAsync(employee, request, userId, isNew: true, cancellationToken);
    }

    public async Task<OperationResult<EmployeeDto>> UpdateAsync(
        Guid id,
        SaveEmployeeRequest request,
        string? userId,
        CancellationToken cancellationToken)
    {
        var employee = await db.Employees.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (employee is null)
            return OperationResult<EmployeeDto>.Fail("Dipendente non trovato.");
        if (!request.Version.HasValue || request.Version.Value != employee.Version)
            return OperationResult<EmployeeDto>.Fail(ConcurrencyConflict);
        return await SaveAsync(employee, request, userId, isNew: false, cancellationToken);
    }

    public async Task<OperationResult<EmployeeDto>> DeactivateAsync(
        Guid id,
        DeactivateEmployeeRequest request,
        string? userId,
        CancellationToken cancellationToken)
    {
        var employee = await db.Employees.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (employee is null)
            return OperationResult<EmployeeDto>.Fail("Dipendente non trovato.");
        if (employee.Version != request.Version)
            return OperationResult<EmployeeDto>.Fail(ConcurrencyConflict);

        var deactivated = employee.Deactivate(request.EndDate);
        if (!deactivated.Success)
            return OperationResult<EmployeeDto>.Fail(deactivated.Error!);
        AddAudit(employee, AuditActions.EmployeeDeactivated, "Dipendente disattivato.", userId);
        return await PersistAsync(employee, cancellationToken);
    }

    private async Task<OperationResult<EmployeeDto>> SaveAsync(
        Employee employee,
        SaveEmployeeRequest request,
        string? userId,
        bool isNew,
        CancellationToken cancellationToken)
    {
        var updated = employee.Update(
            request.EmployeeNumber,
            request.FirstName,
            request.LastName,
            request.Qualification,
            request.ContractLevel,
            request.ContractType,
            request.AnnualGrossSalary,
            request.AnnualCompanyCost,
            request.HireDate,
            request.EndDate,
            request.IsActive,
            request.Notes);
        if (!updated.Success)
            return OperationResult<EmployeeDto>.Fail(updated.Error!);

        if (isNew)
            db.Employees.Add(employee);
        AddAudit(
            employee,
            isNew ? AuditActions.EmployeeCreated : AuditActions.EmployeeUpdated,
            isNew ? "Dipendente creato." : "Dati dipendente aggiornati.",
            userId);
        return await PersistAsync(employee, cancellationToken);
    }

    private async Task<OperationResult<EmployeeDto>> PersistAsync(
        Employee employee,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return OperationResult<EmployeeDto>.Ok(Map(employee));
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return OperationResult<EmployeeDto>.Fail(ConcurrencyConflict);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation
            && postgres.ConstraintName?.Contains("employee_number", StringComparison.OrdinalIgnoreCase) == true)
        {
            db.ChangeTracker.Clear();
            return OperationResult<EmployeeDto>.Fail("La matricola è già assegnata a un altro dipendente.");
        }
    }

    private void AddAudit(Employee employee, string action, string note, string? userId) =>
        db.DocumentAuditEntries.Add(new DocumentAuditEntry(
            AuditDocumentTypes.Employee,
            employee.Id,
            action,
            note,
            userId,
            currentUser.FullName ?? currentUser.Email ?? userId));

    private static EmployeeDto Map(Employee employee) => new(
        employee.Id,
        employee.EmployeeNumber,
        employee.FirstName,
        employee.LastName,
        employee.Qualification,
        employee.ContractLevel,
        employee.ContractType,
        employee.AnnualGrossSalary,
        employee.AnnualCompanyCost,
        employee.HireDate,
        employee.EndDate,
        employee.IsActive,
        employee.Notes,
        employee.Version);
}
