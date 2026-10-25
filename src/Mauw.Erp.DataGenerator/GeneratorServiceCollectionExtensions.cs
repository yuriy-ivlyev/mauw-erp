using Mauw.Erp.Domain.Services;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Mauw.Erp.DataGenerator;

public static class GeneratorServiceCollectionExtensions
{
    public static IServiceCollection AddDataGenerator(this IServiceCollection services)
    {
        services.AddLogging(builder => builder.AddSimpleConsole(console => console.SingleLine = true));
        services.AddSingleton<ItalianAccountingCalculator>();
        services.AddSingleton<PriceFormulaCalculator>();
        services.AddSingleton<GeneratorRunner>();
        return services;
    }

    public static IServiceCollection AddSimulationDatabase(this IServiceCollection services, SafeDatabase database)
    {
        services.AddSingleton(database);
        services.AddSingleton(_ =>
        {
            var connection = new NpgsqlConnectionStringBuilder(database.ConnectionString) { Database = "postgres" };
            return NpgsqlDataSource.Create(connection.ConnectionString);
        });
        services.AddScoped<DatabaseCoordinator>();
        services.AddDbContext<MauwDbContext>(options => options.UseNpgsql(database.ConnectionString));
        services.AddScoped<SimulationGenerator>();
        return services;
    }
}
