using Mauw.Erp.Domain.Services;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mauw.Erp.DataGenerator;

public static class GeneratorServiceCollectionExtensions
{
    public static IServiceCollection AddDataGenerator(this IServiceCollection services)
    {
        services.AddLogging(builder => builder.AddSimpleConsole(console => console.SingleLine = true));
        services.AddSingleton<ItalianAccountingCalculator>();
        services.AddSingleton<CatalogPriceCalculator>();
        services.AddSingleton<GeneratorRunner>();
        return services;
    }

    public static IServiceCollection AddSimulationDatabase(this IServiceCollection services, SafeDatabase database)
    {
        services.AddDbContext<MauwDbContext>(options => options.UseNpgsql(database.ConnectionString));
        services.AddScoped<SimulationGenerator>();
        return services;
    }
}
