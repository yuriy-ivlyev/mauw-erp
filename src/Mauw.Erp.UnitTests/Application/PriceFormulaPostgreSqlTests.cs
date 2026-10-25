using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Pricing;
using Mauw.Erp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Mauw.Erp.UnitTests.Application;

[Collection(PostgreSqlTestCollection.Name)]
public class PriceFormulaPostgreSqlTests(PostgreSqlContainerFixture postgres)
{
    private static PriceFormulaDefinition Formula(decimal roundingStep, params decimal[] percentages) =>
        new(percentages.Select((p, i) => new PriceFormulaStepDefinition($"Passo {i + 1}", p)).ToList(), roundingStep);

    private static PriceFormula NewFormula(decimal roundingStep, params decimal[] percentages) =>
        PriceFormula.Create(Formula(roundingStep, percentages), "admin").Data!;

    [Fact]
    public async Task ReadingReturnsNullUntilAFormulaIsSaved()
    {
        await using var db = postgres.CreateDbContext();
        var repository = new PriceFormulaRepository(db);

        Assert.Null(await repository.GetAsync(CancellationToken.None));
        Assert.Null(await repository.GetForUpdateAsync(CancellationToken.None));
    }

    public static TheoryData<decimal, decimal[]> PrecisionCases => new()
    {
        { 0.10m, new[] { 40.00m, 10.00m, 5.00m } },
        { 0.05m, new[] { 0.01m, 9999.00m } },
        { 100.00m, new[] { 0.00m } },
        { 0.50m, new[] { 12.34m, 56.78m, 0.01m, 99.99m } }
    };

    [Theory]
    [MemberData(nameof(PrecisionCases))]
    public async Task SaveAndReloadKeepsDecimalPrecisionAndStepOrder(decimal roundingStep, decimal[] percentages)
    {
        await using var db = postgres.CreateDbContext();
        var repository = new PriceFormulaRepository(db);
        var formula = NewFormula(roundingStep, percentages);
        await repository.AddAsync(formula, CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);
        db.ChangeTracker.Clear();

        var loaded = await repository.GetAsync(CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(formula.Id, loaded.Id);
        Assert.Equal(roundingStep, loaded.RoundingStep);
        Assert.Equal("admin", loaded.UpdatedByUserId);
        Assert.Equal(Enumerable.Range(1, percentages.Length), loaded.Steps.Select(step => step.Position));
        Assert.Equal(percentages, loaded.Steps.Select(step => step.Percentage));
        Assert.Equal(percentages.Select((_, i) => $"Passo {i + 1}"), loaded.Steps.Select(step => step.Label));
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task StepsAreReturnedOrderedByPositionRegardlessOfInsertionOrder()
    {
        var database = postgres.CreateDatabase();
        Guid formulaId;
        await using (var db = database.CreateDbContext())
        {
            var formula = NewFormula(0.10m, 40.00m, 10.00m, 5.00m);
            formulaId = formula.Id;
            db.Add(formula);
            await db.SaveChangesAsync();
        }
        await using (var db = database.CreateDbContext())
        {
            // Riscrive le posizioni in modo che l'ordine fisico non coincida con quello logico.
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE price_formula_steps SET \"position\" = 100 - \"position\" WHERE price_formula_id = {0}", formulaId);
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE price_formula_steps SET \"position\" = \"position\" - 96 WHERE price_formula_id = {0}", formulaId);
        }
        await using (var db = database.CreateDbContext())
        {
            var loaded = await new PriceFormulaRepository(db).GetAsync(CancellationToken.None);

            Assert.Equal([1, 2, 3], loaded!.Steps.Select(step => step.Position));
            Assert.Equal([5.00m, 10.00m, 40.00m], loaded.Steps.Select(step => step.Percentage));
        }
    }

    [Fact]
    public async Task UpdateReplacesStepsAndPersistsNewValues()
    {
        var database = postgres.CreateDatabase();
        Guid formulaId;
        await using (var db = database.CreateDbContext())
        {
            var repository = new PriceFormulaRepository(db);
            var formula = NewFormula(0.10m, 40.00m, 10.00m, 5.00m);
            formulaId = formula.Id;
            await repository.AddAsync(formula, CancellationToken.None);
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        await using (var db = database.CreateDbContext())
        {
            var repository = new PriceFormulaRepository(db);
            var tracked = await repository.GetForUpdateAsync(CancellationToken.None);
            var result = tracked!.Update(Formula(0.50m, 25.50m, 3.25m), "other");
            Assert.True(result.Success, result.Error);
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        await using (var db = database.CreateDbContext())
        {
            var loaded = await new PriceFormulaRepository(db).GetAsync(CancellationToken.None);

            Assert.Equal(formulaId, loaded!.Id);
            Assert.Equal(0.50m, loaded.RoundingStep);
            Assert.Equal("other", loaded.UpdatedByUserId);
            Assert.Equal([1, 2], loaded.Steps.Select(step => step.Position));
            Assert.Equal([25.50m, 3.25m], loaded.Steps.Select(step => step.Percentage));
            Assert.Equal(1, await db.PriceFormulas.CountAsync());
            Assert.Equal(2, await db.PriceFormulaSteps.CountAsync());
        }
    }

    [Fact]
    public async Task UpdateToAFewerStepsDeletesTheRemovedOnes()
    {
        var database = postgres.CreateDatabase();
        await using (var db = database.CreateDbContext())
        {
            db.Add(NewFormula(0.10m, 40.00m, 10.00m, 5.00m));
            await db.SaveChangesAsync();
        }
        await using (var db = database.CreateDbContext())
        {
            var repository = new PriceFormulaRepository(db);
            var tracked = await repository.GetForUpdateAsync(CancellationToken.None);
            Assert.True(tracked!.Update(Formula(0.10m, 7.00m), null).Success);
            await repository.SaveChangesAsync(CancellationToken.None);
        }
        await using (var db = database.CreateDbContext())
        {
            var loaded = await new PriceFormulaRepository(db).GetAsync(CancellationToken.None);

            Assert.Equal([7.00m], loaded!.Steps.Select(step => step.Percentage));
            Assert.Null(loaded.UpdatedByUserId);
            Assert.Equal(1, await db.PriceFormulaSteps.CountAsync());
        }
    }

    [Fact]
    public async Task SecondFormulaRowIsRejectedByTheDatabase()
    {
        var database = postgres.CreateDatabase();
        await using (var db = database.CreateDbContext())
        {
            db.Add(NewFormula(0.10m, 40.00m));
            await db.SaveChangesAsync();
        }

        await using (var db = database.CreateDbContext())
        {
            db.Add(NewFormula(0.50m, 10.00m));
            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            var postgresError = Assert.IsType<PostgresException>(exception.InnerException);
            Assert.Equal(PostgresErrorCodes.UniqueViolation, postgresError.SqlState);
            Assert.Equal("ux_price_formulas_is_singleton", postgresError.ConstraintName);
        }

        await using (var db = database.CreateDbContext())
            Assert.Equal(1, await db.PriceFormulas.CountAsync());
    }

    [Fact]
    public async Task SingletonColumnCannotBeFalseToBypassTheUniqueIndex()
    {
        var database = postgres.CreateDatabase();
        await using var db = database.CreateDbContext();
        db.Add(NewFormula(0.10m, 40.00m));
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO price_formulas (id, rounding_step, is_singleton, created_at, updated_at) " +
            "VALUES (gen_random_uuid(), 0.10, false, now(), now())"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
    }

    [Fact]
    public async Task DuplicateFormulaPositionIsRejectedByTheDatabase()
    {
        var database = postgres.CreateDatabase();
        await using var db = database.CreateDbContext();
        var formula = NewFormula(0.10m, 40.00m, 10.00m);
        db.Add(formula);
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE price_formula_steps SET \"position\" = 1 WHERE price_formula_id = {0} AND \"position\" = 2",
            formula.Id));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, exception.SqlState);
        Assert.Equal("ux_price_formula_steps_price_formula_id_position", exception.ConstraintName);
    }

    public static TheoryData<int, decimal> OutOfRangeSteps => new()
    {
        { 0, 10.00m },
        { 2, -0.01m },
        { 2, 9999.01m }
    };

    [Theory]
    [MemberData(nameof(OutOfRangeSteps))]
    public async Task StepCheckConstraintsRejectOutOfRangeValues(int position, decimal percentage)
    {
        var database = postgres.CreateDatabase();
        await using var db = database.CreateDbContext();
        var formula = NewFormula(0.10m, 40.00m);
        db.Add(formula);
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO price_formula_steps (id, price_formula_id, \"position\", label, percentage, created_at, updated_at) " +
            "VALUES (gen_random_uuid(), {0}, {1}, 'x', {2}, now(), now())",
            formula.Id, position, percentage));
        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
    }

    [Fact]
    public async Task DeletingTheFormulaCascadesToItsSteps()
    {
        var database = postgres.CreateDatabase();
        await using (var db = database.CreateDbContext())
        {
            db.Add(NewFormula(0.10m, 40.00m, 10.00m, 5.00m));
            await db.SaveChangesAsync();
        }
        await using (var db = database.CreateDbContext())
        {
            var deleted = await db.PriceFormulas.ExecuteDeleteAsync();

            Assert.Equal(1, deleted);
            Assert.Equal(0, await db.PriceFormulaSteps.CountAsync());
        }
    }

    [Fact]
    public async Task FormulaCanBeRecreatedAfterDeletion()
    {
        var database = postgres.CreateDatabase();
        await using var db = database.CreateDbContext();
        db.Add(NewFormula(0.10m, 40.00m));
        await db.SaveChangesAsync();
        await db.PriceFormulas.ExecuteDeleteAsync();
        db.ChangeTracker.Clear();

        db.Add(NewFormula(0.50m, 5.00m));
        await db.SaveChangesAsync();

        Assert.Equal(1, await db.PriceFormulas.CountAsync());
    }
}
