using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Services;

public interface ICompanySettingsService
{
    Task<OperationResult<CompanySettingsDto>> GetCompanyAsync();
    Task<OperationResult<CompanySettingsDto>> UpsertCompanyAsync(UpdateCompanySettingsRequest request);
    Task<OperationResult<CompanyAddressSiteDto>> AddOperationalSiteAsync(CompanyAddressSiteRequest request);
    Task<OperationResult<CompanyAddressSiteDto>> UpdateOperationalSiteAsync(Guid siteId, CompanyAddressSiteRequest request);
    Task<OperationResult<bool>> DeleteOperationalSiteAsync(Guid siteId);
}
