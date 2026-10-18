using Mauw.Erp.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Mauw.Erp.Infrastructure.Branding;

public static class BrandingServiceCollectionExtensions
{
    public static IServiceCollection AddCompanyBranding(this IServiceCollection services)
    {
        services.AddScoped<ICompanyLogoService, CompanyLogoService>();
        return services;
    }
}
