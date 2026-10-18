using Mauw.Erp.Contracts.Interfaces;
using Mauw.Erp.Infrastructure.Reporting.MigraDoc;
using Microsoft.Extensions.DependencyInjection;
using PdfSharp.Fonts;

namespace Mauw.Erp.Infrastructure.Reporting;

public static class ReportingServiceCollectionExtensions
{
    private static readonly object FontSettingsLock = new();

    public static IServiceCollection AddInvoicePdfReporting(this IServiceCollection services)
    {
        lock (FontSettingsLock)
        {
            GlobalFontSettings.FontResolver ??= new EmbeddedFontResolver();
        }

        services.AddScoped<IInvoicePdfGenerator, MigraDocInvoicePdfGenerator>();
        return services;
    }
}
