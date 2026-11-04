using Mauw.Erp.Application.Services;
using Mauw.Erp.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Mauw.Erp.Infrastructure.Storage;

public static class PayableDocumentServiceCollectionExtensions
{
    public static IServiceCollection AddPayableDocuments(this IServiceCollection services)
    {
        services.AddScoped<IPayableDocumentService, PayableDocumentService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IEmployeeDocumentService, EmployeeDocumentService>();
        services.AddScoped<IEmployeeTimeEntryService, EmployeeTimeEntryService>();
        return services;
    }
}
