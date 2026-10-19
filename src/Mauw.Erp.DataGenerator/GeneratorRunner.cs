using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Mauw.Erp.DataGenerator;

public sealed class GeneratorRunner(ILogger<GeneratorRunner> logger)
{
    private const long LockId = 749283105;

    public async Task<int> RunAsync(string[] args, CancellationToken token)
    {
        var parsed = GeneratorCommand.Parse(args);
        if (!parsed.Success)
        {
            logger.LogError("{Error}", parsed.Error);
            return 2;
        }
        var command = parsed.Data!;
        if (command.Action == "help")
        {
            logger.LogInformation("Comandi: plan | seed --confirm-database NOME | reset --confirm-database NOME --allow-reset. Connessione: MAUW_LOADTEST_CONNECTION. Opzioni: --end-date yyyy-MM-dd --years --seed --customers --active-customers --suppliers --agents --active-agents --products --initial-products --batch-size. Manuale: src/Mauw.Erp.DataGenerator/README.txt");
            return 0;
        }
        logger.LogInformation("Scenario: {@Options}", command.Options);
        if (command.Action == "plan")
            return 0;

        var safe = DatabaseSafety.Validate(Environment.GetEnvironmentVariable("MAUW_LOADTEST_CONNECTION"), command.Confirmation);
        if (!safe.Success)
        {
            logger.LogError("{Error}", safe.Error);
            return 2;
        }

        using var services = new ServiceCollection().AddDataGenerator().AddSimulationDatabase(safe.Data!).BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MauwDbContext>();
        try
        {
            await db.Database.OpenConnectionAsync(token);
            await using var connectionLock = db.Database.GetDbConnection().CreateCommand();
            connectionLock.CommandText = $"SELECT pg_try_advisory_lock({LockId})";
            if (await connectionLock.ExecuteScalarAsync(token) is not true)
            {
                logger.LogError("Un altro generatore sta usando questo database.");
                return 2;
            }

            if (command.Action == "reset")
            {
                // FORCE is deliberately not used: stop active application sessions first.
                await db.Database.CloseConnectionAsync();
                await db.Database.EnsureDeletedAsync(token);
                logger.LogInformation("Database eliminato. Ricrearlo con dotnet ef database update, poi eseguire seed (vedere README.txt).");
                return 0;
            }
            if ((await db.Database.GetPendingMigrationsAsync(token)).Any())
            {
                logger.LogError("Migrazioni mancanti: eseguire prima dotnet ef database update sul database dedicato.");
                return 2;
            }
            if (await db.Contacts.IgnoreQueryFilters().AnyAsync(token) ||
                await db.Products.AnyAsync(token) ||
                await db.StockMovements.AnyAsync(token) ||
                await db.Orders.AnyAsync(token) ||
                await db.Invoices.AnyAsync(token) ||
                await db.DdtDocuments.AnyAsync(token))
            {
                logger.LogError("Database gia popolato: seed richiede dati operativi vuoti. Nessun dato modificato; usare reset per ripartire.");
                return 2;
            }
            var generated = await scope.ServiceProvider.GetRequiredService<SimulationGenerator>().GenerateAsync(command.Options, token);
            if (!generated.Success)
            {
                logger.LogError("Generazione interrotta: {Error}. I batch precedenti possono essere presenti: ripartire con reset.", generated.Error);
                return 2;
            }
            logger.LogInformation("Generazione completata: {@Summary}", generated.Data);
            return 0;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            logger.LogWarning("Operazione annullata. I batch completati restano nel database; usare reset prima di ripetere.");
            return 130;
        }
        catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException or DbUpdateException)
        {
            // Provider messages can include connection information or generated data.
            logger.LogError("Operazione database non riuscita ({ErrorType}). Verificare connessione, migrazioni e permessi. Per ripartire usare reset.", ex.GetType().Name);
            return 1;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
