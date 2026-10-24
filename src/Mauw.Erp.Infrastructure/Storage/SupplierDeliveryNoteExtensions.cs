using Mauw.Erp.Application.Services;
using Mauw.Erp.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Mauw.Erp.Infrastructure.Storage;

public static class SupplierDeliveryNoteExtensions
{
    public static IServiceCollection AddSupplierDeliveryNotes(this IServiceCollection services)
    {
        services.AddSingleton<ISupplierDocumentStorage, S3SupplierDocumentStorage>();
        services.AddScoped<ISupplierDeliveryNoteService, SupplierDeliveryNoteService>();
        services.AddScoped<ISupplierPurchaseOrderService, SupplierPurchaseOrderService>();
        return services;
    }
}
