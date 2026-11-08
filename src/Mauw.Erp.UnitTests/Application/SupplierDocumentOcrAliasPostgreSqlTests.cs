using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.UnitTests.Application;

[Collection(PostgreSqlTestCollection.Name)]
public sealed class SupplierDocumentOcrAliasPostgreSqlTests(PostgreSqlContainerFixture postgres)
{
    [Fact]
    public async Task EnsureCreatedSeedsGlobalAliasesForEveryOcrColumn()
    {
        var database = postgres.CreateDatabase();
        await using var db = database.CreateDbContext();
        var service = new SupplierDocumentOcrAliasService(db);

        var aliases = await service.ListAsync(CancellationToken.None);

        Assert.Contains(aliases, alias => alias.Column == "SupplierCode" && alias.Alias == "codicearticolo");
        Assert.Contains(aliases, alias => alias.Column == "Description" && alias.Alias == "descrizione");
        Assert.Contains(aliases, alias => alias.Column == "Quantity" && alias.Alias == "quantita");
        Assert.Contains(aliases, alias => alias.Column == "Quantity" && alias.Alias == "pz");
    }

    [Fact]
    public async Task AddNormalizesAliasesAndRejectsDuplicatesAcrossFields()
    {
        var database = postgres.CreateDatabase();
        await using var db = database.CreateDbContext();
        var service = new SupplierDocumentOcrAliasService(db);

        var added = await service.AddAsync(
            new CreateSupplierDocumentOcrHeaderAliasRequest("SupplierCode", " Rif. Art. "),
            CancellationToken.None);
        var duplicate = await service.AddAsync(
            new CreateSupplierDocumentOcrHeaderAliasRequest("Description", "rifart"),
            CancellationToken.None);

        Assert.True(added.Success, added.Error);
        Assert.Equal("Rif. Art.", added.Data!.Alias);
        Assert.False(duplicate.Success);
        Assert.Contains("già configurato", duplicate.Error);
    }

    [Fact]
    public async Task DeleteRejectsRemovingTheLastAliasForAColumn()
    {
        var database = postgres.CreateDatabase();
        await using var db = database.CreateDbContext();
        var service = new SupplierDocumentOcrAliasService(db);
        var alias = await db.SupplierDocumentOcrHeaderAliases
            .Where(item => item.Column.ToString() == "Quantity")
            .OrderBy(item => item.Position)
            .ToListAsync();

        var lastAlias = alias[^1];
        foreach (var item in alias.Take(alias.Count - 1))
            Assert.True((await service.DeleteAsync(item.Id, CancellationToken.None)).Success);

        var result = await service.DeleteAsync(lastAlias.Id, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("almeno un alias", result.Error);
    }
}
