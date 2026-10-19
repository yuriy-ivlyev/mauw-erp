using Mauw.Erp.DataGenerator;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Mauw.Erp.UnitTests.DataGenerator;

public class GeneratorSafetyTests
{
    [Fact]
    public void ResolvesGeneratorWithoutConnectingToDatabase()
    {
        var safe = DatabaseSafety.Validate("Host=localhost;Database=mauw_erp_loadtest", "mauw_erp_loadtest");
        using var services = new ServiceCollection().AddDataGenerator()
            .AddSimulationDatabase(safe.Data!)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<GeneratorRunner>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<SimulationGenerator>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<DatabaseCoordinator>());
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "mauw_erp_loadtest")]
    [InlineData("not a connection", "mauw_erp_loadtest")]
    [InlineData("Host=localhost;Database=mauw_erp", "mauw_erp")]
    [InlineData("Host=localhost;Database=postgres", "postgres")]
    [InlineData("Host=localhost;Database=other_loadtest", "mauw_erp_loadtest")]
    [InlineData("Database=mauw_erp_loadtest", "mauw_erp_loadtest")]
    [InlineData("Host=localhost;Database=mauw_erp_LOADTEST", "mauw_erp_LOADTEST")]
    [InlineData("Host=localhost;Database=unsafe-name_loadtest", "unsafe-name_loadtest")]
    public void RejectsUnconfirmedOrNonDedicatedDatabase(string? connection, string? confirmation)
    {
        Assert.False(DatabaseSafety.Validate(connection, confirmation).Success);
    }

    [Fact]
    public void AcceptsDedicatedDatabaseAndDisablesSensitiveDiagnostics()
    {
        var result = DatabaseSafety.Validate(
            "Host=localhost;Database=mauw_erp_loadtest;Include Error Detail=true;Log Parameters=true;Multiplexing=true",
            "mauw_erp_loadtest");
        Assert.True(result.Success, result.Error);
        var connection = new NpgsqlConnectionStringBuilder(result.Data!.ConnectionString);
        Assert.False(connection.IncludeErrorDetail);
        Assert.False(connection.LogParameters);
        Assert.False(connection.Multiplexing);
        Assert.False(connection.Pooling);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("seed --years")]
    [InlineData("seed --years invalid")]
    [InlineData("seed --end-date 19/10/2026")]
    [InlineData("seed --unknown 5")]
    [InlineData("seed --seed 42 --seed 43")]
    [InlineData("reset --confirm-database mauw_erp_loadtest")]
    [InlineData("seed --allow-reset")]
    public void RejectsInvalidCommandBeforeDatabaseAccess(string arguments)
    {
        Assert.False(GeneratorCommand.Parse(arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Success);
    }

    [Fact]
    public void AcceptsExplicitReset()
    {
        var result = GeneratorCommand.Parse(["reset", "--confirm-database", "mauw_erp_loadtest", "--allow-reset"]);
        Assert.True(result.Success, result.Error);
        Assert.True(result.Data!.AllowReset);
    }

    [Fact]
    public void FixedDateAndSeedArePreserved()
    {
        var result = GeneratorCommand.Parse(["plan", "--years", "10", "--end-date", "2026-10-19", "--seed", "123"]);
        Assert.True(result.Success, result.Error);
        Assert.Equal(10, result.Data!.Options.Years);
        Assert.Equal(new DateOnly(2026, 10, 19), result.Data.Options.EndDate);
        Assert.Equal(123, result.Data.Options.Seed);
    }
}
