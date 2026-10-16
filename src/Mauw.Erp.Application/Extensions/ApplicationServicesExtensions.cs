using Mauw.Erp.Application.Decorators;
using Mauw.Erp.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Mauw.Erp.Application.Extensions;

public static class ApplicationServicesExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<OrderService>();
        services.AddScoped<IOrderService>(sp =>
            new TracingOrderService(sp.GetRequiredService<OrderService>()));

        services.AddScoped<ProductService>();
        services.AddScoped<IProductService>(sp =>
            new TracingProductService(sp.GetRequiredService<ProductService>()));

        services.AddScoped<ContactService>();
        services.AddScoped<IContactService>(sp =>
            new TracingContactService(sp.GetRequiredService<ContactService>()));

        services.AddScoped<InvoiceService>();
        services.AddScoped<IInvoiceService>(sp =>
            new TracingInvoiceService(sp.GetRequiredService<InvoiceService>()));

        services.AddScoped<InventoryService>();
        services.AddScoped<IInventoryService>(sp =>
            new TracingInventoryService(sp.GetRequiredService<InventoryService>()));

        services.AddScoped<DdtService>();
        services.AddScoped<IDdtService>(sp =>
            new TracingDdtService(sp.GetRequiredService<DdtService>()));

        // Company settings (singleton-ish via IMemoryCache)
        services.AddScoped<ICompanySettingsService, CompanySettingsService>();

        // Contact address CRUD
        services.AddScoped<IContactAddressService, ContactAddressService>();

        return services;
    }
}
