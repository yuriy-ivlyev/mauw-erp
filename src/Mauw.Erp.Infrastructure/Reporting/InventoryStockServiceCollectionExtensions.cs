using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Interfaces;
using Mauw.Erp.Infrastructure.Reporting.MigraDoc;
using Mauw.Erp.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using PdfSharp.Fonts;

namespace Mauw.Erp.Infrastructure.Reporting;

public static class InventoryStockServiceCollectionExtensions
{
    public static IServiceCollection AddInventoryStockReporting(this IServiceCollection services)
    {
        lock (typeof(InventoryStockServiceCollectionExtensions))
        {
            GlobalFontSettings.FontResolver ??= new EmbeddedFontResolver();
        }

        services.AddScoped<IInventoryStockQueryService, InventoryStockQueryService>();
        services.AddScoped<IInventoryStockPdfGenerator, MigraDocInventoryStockPdfGenerator>();
        return services;
    }
}
