using System.Buffers.Binary;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Infrastructure.Branding;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos.Pdf;
using Mauw.Erp.Contracts.Interfaces;
using Mauw.Erp.Infrastructure.Reporting;
using Mauw.Erp.UnitTests.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Mauw.Erp.UnitTests.Branding;

[Collection(PostgreSqlTestCollection.Name)]
public class CompanyLogoTests(PostgreSqlContainerFixture postgres)
{
    private MauwDbContext CreateDb() => postgres.CreateDbContext();

    [Theory]
    [InlineData("horizontal")]
    [InlineData("compact")]
    public void EmbeddedLogosAreReadablePngs(string name)
    {
        Assert.Null(CompanyLogoService.ValidatePng(DefaultCompanyLogos.ReadPng(name)));
    }

    [Fact]
    public void CompactDefaultUsesTextlessSymbolProportions()
    {
        var png = DefaultCompanyLogos.ReadPng("compact");

        Assert.Equal(800u, BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16, 4)));
        Assert.Equal(736u, BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20, 4)));
    }

    [Fact]
    public async Task SeedFillsMissingLogoWithoutOverwritingReplacement()
    {
        await using var db = CreateDb();
        var replacement = DefaultCompanyLogos.ReadPng("compact");
        var company = new Contact("Mauw") { IsInternalCompany = true, HorizontalLogoPng = replacement };
        db.Contacts.Add(company);
        await db.SaveChangesAsync();

        await DbInitializer.SeedCompanyLogosAsync(db);
        await DbInitializer.SeedCompanyLogosAsync(db);

        Assert.Equal(replacement, company.HorizontalLogoPng);
        Assert.Equal(DefaultCompanyLogos.ReadPng("compact"), company.CompactLogoPng);
        Assert.Single(db.Contacts);
    }

    [Fact]
    public async Task ReplacementPersistsOnInternalCompanyAndDoesNotTouchOtherContacts()
    {
        await using var db = CreateDb();
        var company = new Contact("Mauw") { IsInternalCompany = true };
        var customer = new Contact("Cliente");
        db.AddRange(company, customer);
        await db.SaveChangesAsync();
        await DbInitializer.SeedCompanyLogosAsync(db);
        var compact = company.CompactLogoPng;
        var service = new CompanyLogoService(db);
        var replacement = DefaultCompanyLogos.ReadPng("compact");

        var result = await service.UpdateAsync("horizontal", new(replacement));
        db.ChangeTracker.Clear();
        var loaded = await new CompanyLogoService(db).GetAsync("horizontal");

        Assert.True(result.Success, result.Error);
        Assert.True(loaded.Success, loaded.Error);
        Assert.Equal(replacement, loaded.Data!.PngData);
        Assert.Equal(compact, (await db.Contacts.SingleAsync(c => c.IsInternalCompany)).CompactLogoPng);
        Assert.Null((await db.Contacts.SingleAsync(c => !c.IsInternalCompany)).HorizontalLogoPng);
    }

    [Fact]
    public async Task InvalidUploadDoesNotChangeStoredLogo()
    {
        await using var db = CreateDb();
        var company = new Contact("Mauw") { IsInternalCompany = true };
        db.Add(company);
        await db.SaveChangesAsync();
        await DbInitializer.SeedCompanyLogosAsync(db);
        var original = company.HorizontalLogoPng!.ToArray();
        var service = new CompanyLogoService(db);

        var result = await service.UpdateAsync("horizontal", new([1, 2, 3]));

        Assert.False(result.Success);
        Assert.Equal(original, company.HorizontalLogoPng);
        Assert.False((await service.UpdateAsync("unknown", new(original))).Success);
    }

    [Theory]
    [InlineData("TD01")]
    [InlineData("ORDINE")]
    [InlineData("DDT")]
    [InlineData("NOTA CREDITO")]
    public async Task NewPdfUsesReplacementWithoutRecreatingGenerator(string documentType)
    {
        await using var db = CreateDb();
        db.Add(new Contact("Mauw") { IsInternalCompany = true });
        await db.SaveChangesAsync();
        await DbInitializer.SeedCompanyLogosAsync(db);
        var logos = new CompanyLogoService(db);
        var services = new ServiceCollection();
        services.AddSingleton<ICompanyLogoService>(logos);
        services.AddInvoicePdfReporting();
        using var provider = services.BuildServiceProvider();
        var generator = provider.GetRequiredService<IInvoicePdfGenerator>();
        var model = new InvoicePdfModel(
            "LOGO-TEST", documentType, new DateOnly(2026, 10, 18), "EUR",
            new("Mauw", "00000000000", null, "Via Roma 1", "Roma", "00100", "RM",
                null, null, "RF01", null, null),
            new("Cliente", null, null, "Via Milano 2", "Roma", "00100", "RM", null, null),
            new("A vista", "MP01", null, null, null, null),
            [new("SKU", "Articolo", null, 1, 10.00m, null, 22m, 10.00m)],
            [new(22m, 10.00m, 2.20m)], new(1, 10.00m, 2.20m, 12.20m));
        using var original = UglyToad.PdfPig.PdfDocument.Open(await generator.GenerateInvoicePdfAsync(model));
        Assert.Equal(800, Assert.Single(original.GetPage(1).GetImages()).WidthInSamples);

        Assert.True((await logos.UpdateAsync("compact", new(DefaultCompanyLogos.ReadPng("horizontal")))).Success);

        using var updated = UglyToad.PdfPig.PdfDocument.Open(await generator.GenerateInvoicePdfAsync(model));
        Assert.Equal(1200, Assert.Single(updated.GetPage(1).GetImages()).WidthInSamples);
    }

    [Fact]
    public void RejectsOversizedFilesInvalidSignaturesAndExcessiveDimensions()
    {
        Assert.NotNull(CompanyLogoService.ValidatePng(new byte[2 * 1024 * 1024 + 1]));
        Assert.NotNull(CompanyLogoService.ValidatePng(new byte[33]));
        var png = DefaultCompanyLogos.ReadPng("horizontal");
        BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(16, 4), 100_000);
        Assert.NotNull(CompanyLogoService.ValidatePng(png));
    }
}
