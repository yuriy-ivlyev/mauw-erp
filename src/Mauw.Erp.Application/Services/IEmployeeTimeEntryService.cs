using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Services;

public interface IEmployeeTimeEntryService
{
    Task<PagedResult<EmployeeTimeEntryDto>> ListAsync(
        Guid employeeId,
        DateOnly from,
        DateOnly to,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<OperationResult<EmployeeTimeEntryDto>> CreateAsync(
        Guid employeeId,
        SaveEmployeeTimeEntryRequest request,
        string? userId,
        CancellationToken cancellationToken);

    Task<OperationResult<EmployeeTimeEntryDto>> UpdateAsync(
        Guid employeeId,
        Guid entryId,
        SaveEmployeeTimeEntryRequest request,
        string? userId,
        CancellationToken cancellationToken);
}
