using Mauw.Erp.Domain.Accounting;
using Npgsql;

namespace Mauw.Erp.DataGenerator;

public sealed class DatabaseCoordinator(NpgsqlDataSource maintenance, SafeDatabase database)
{
    public async Task<AccountingResult<NpgsqlConnection>> AcquireAsync(CancellationToken token)
    {
        var connection = await maintenance.OpenConnectionAsync(token);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT pg_try_advisory_lock(hashtextextended(@database, 749283105))";
            command.Parameters.AddWithValue("database", database.DatabaseName);
            if (await command.ExecuteScalarAsync(token) is true)
                return AccountingResult<NpgsqlConnection>.Ok(connection);
            await connection.DisposeAsync();
            return AccountingResult<NpgsqlConnection>.Fail("Un altro generatore sta usando questo database.");
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public async Task ResetAsync(NpgsqlConnection lease, CancellationToken token)
    {
        await using var command = lease.CreateCommand();
        var identifier = database.DatabaseName.Replace("\"", "\"\"");
        // Unlike EnsureDeleted, this never disconnects active clients with WITH (FORCE).
        command.CommandText = $"DROP DATABASE IF EXISTS \"{identifier}\"";
        await command.ExecuteNonQueryAsync(token);
    }
}
