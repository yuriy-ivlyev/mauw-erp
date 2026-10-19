using Mauw.Erp.Domain.Accounting;
using Npgsql;

namespace Mauw.Erp.DataGenerator;

public sealed record SafeDatabase(string ConnectionString, string DatabaseName);

public static class DatabaseSafety
{
    public static AccountingResult<SafeDatabase> Validate(string? connectionString, string? confirmation)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return AccountingResult<SafeDatabase>.Fail("Impostare MAUW_LOADTEST_CONNECTION: nessun collegamento al database applicativo viene ereditato.");

        NpgsqlConnectionStringBuilder connection;
        try
        {
            connection = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            return AccountingResult<SafeDatabase>.Fail("Stringa di connessione PostgreSQL non valida.");
        }

        var name = connection.Database;
        if (string.IsNullOrWhiteSpace(connection.Host) ||
            string.IsNullOrWhiteSpace(name) ||
            name.Length > 63 ||
            !name.EndsWith("_loadtest", StringComparison.Ordinal) ||
            name.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
            return AccountingResult<SafeDatabase>.Fail("Usare un host esplicito e un database dedicato con nome ASCII terminante in _loadtest (massimo 63 caratteri).");
        if (!string.Equals(name, confirmation, StringComparison.Ordinal))
            return AccountingResult<SafeDatabase>.Fail("Confermare il nome esatto con --confirm-database NOME_DATABASE.");

        // Keep advisory locks confined to this physical connection.
        connection.Multiplexing = false;
        connection.Pooling = false;
        connection.IncludeErrorDetail = false;
        connection.LogParameters = false;
        return AccountingResult<SafeDatabase>.Ok(new SafeDatabase(connection.ConnectionString, name));
    }
}
