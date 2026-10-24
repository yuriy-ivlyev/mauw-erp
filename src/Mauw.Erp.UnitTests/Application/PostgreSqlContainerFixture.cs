using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Mauw.Erp.UnitTests.Application;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgreSqlTestCollection : ICollectionFixture<PostgreSqlContainerFixture>
{
    public const string Name = "PostgreSQL integration tests";
}

public sealed class PostgreSqlContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public MauwDbContext CreateDbContext(params IInterceptor[] interceptors) =>
        CreateDatabase().CreateDbContext(interceptors);

    public PostgreSqlTestDatabase CreateDatabase()
    {
        var databaseName = $"test_{Guid.NewGuid():N}";
        var adminConnectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Pooling = false
        }.ConnectionString;
        using (var connection = new NpgsqlConnection(adminConnectionString))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
            command.ExecuteNonQuery();
        }

        var connectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = databaseName,
            Pooling = false
        }.ConnectionString;
        var database = new PostgreSqlTestDatabase(connectionString);
        using (var db = database.CreateDbContext())
            db.Database.EnsureCreated();

        return database;
    }
}

public sealed class PostgreSqlTestDatabase(string connectionString)
{
    public MauwDbContext CreateDbContext(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<MauwDbContext>().UseNpgsql(connectionString);
        if (interceptors.Length > 0)
            options.AddInterceptors(interceptors);
        return new MauwDbContext(options.Options);
    }
}
