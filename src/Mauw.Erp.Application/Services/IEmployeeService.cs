using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Services;

public interface IEmployeeService
{
    Task<PagedResult<EmployeeDto>> ListAsync(
        int page,
        int pageSize,
        string? search,
        bool activeOnly,
        CancellationToken cancellationToken);

    Task<EmployeeDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<OperationResult<EmployeeDto>> CreateAsync(
        SaveEmployeeRequest request,
        string? userId,
        CancellationToken cancellationToken);

    Task<OperationResult<EmployeeDto>> UpdateAsync(
        Guid id,
        SaveEmployeeRequest request,
        string? userId,
        CancellationToken cancellationToken);

    Task<OperationResult<EmployeeDto>> DeactivateAsync(
        Guid id,
        DeactivateEmployeeRequest request,
        string? userId,
        CancellationToken cancellationToken);
}
