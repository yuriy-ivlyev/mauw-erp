using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Services;

public interface ICompanyLogoService
{
    Task<OperationResult<CompanyLogoDto>> GetAsync(string name, CancellationToken ct = default);
    Task<OperationResult<bool>> UpdateAsync(string name, UpdateCompanyLogoRequest request, CancellationToken ct = default);
}
