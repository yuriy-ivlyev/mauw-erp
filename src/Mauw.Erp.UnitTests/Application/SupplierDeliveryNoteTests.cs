using System.Text;
using System.Text.Json;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Infrastructure.DbContext;
using Mauw.Erp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

[Collection(PostgreSqlTestCollection.Name)]
public class SupplierDeliveryNoteTests(PostgreSqlContainerFixture postgres)
{
    [Fact]
    public void FractionalQuantityIsRejectedByTheRequestContract()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SupplierDeliveryNoteLineRequest>(
            """{"Quantity":1.5,"UnitPrice":"12.34567890"}"""));
        var line = JsonSerializer.Deserialize<SupplierDeliveryNoteLineRequest>(
            """{"Quantity":5,"UnitPrice":"12.34567890"}""")!;
        Assert.Equal(5, line.Quantity);
        Assert.Equal(12.34567890m, line.UnitPrice);
        Assert.Equal(typeof(int), typeof(SupplierDeliveryNoteLine).GetProperty("Quantity")!.PropertyType);
        Assert.Equal(typeof(int), typeof(StockMovement).GetProperty("Quantity")!.PropertyType);
    }

    [Theory]
    [InlineData("application/pdf", "doc.pdf", "%PDF-1.7 test")]
    [InlineData("image/jpeg", "doc.jpeg", "\u00ff\u00d8\u00ff")]
    public async Task UploadArchivesTheOriginalAndReturnsDraft(string type, string name, string contents)
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        using var stream = new MemoryStream(Encoding.Latin1.GetBytes(contents));
        var result = await fixture.Service.UploadAsync(
            new(fixture.Supplier.Id, fixture.Warehouse.Id, null, DateTimeOffset.UtcNow),
            stream, name, type, stream.Length, default);
        Assert.True(result.Success, result.Error);
        Assert.Equal("Draft", result.Data!.Status);
        Assert.Equal("", result.Data.DocumentNumber);
        Assert.Equal("Fornitore test", result.Data.SupplierName);
        fixture.Storage.Verify(x => x.UploadAsync(result.Data.Id, It.IsAny<Stream>(), type, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(fixture.Db.StockMovements);
    }

    [Fact]
    public async Task UploadRejectsMismatchedSignatureAndOversizedFilesWithoutStorageWrites()
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        using var stream = new MemoryStream("not a pdf"u8.ToArray());
        var request = new UploadSupplierDeliveryNoteRequest(
            fixture.Supplier.Id, fixture.Warehouse.Id, "DDT", DateTimeOffset.UtcNow);
        Assert.False((await fixture.Service.UploadAsync(request, stream, "doc.pdf", "application/pdf", stream.Length, default)).Success);
        Assert.False((await fixture.Service.UploadAsync(request, stream, "doc.pdf", "application/pdf",
            SupplierDeliveryNoteService.MaximumFileSize + 1, default)).Success);
        fixture.Storage.VerifyNoOtherCalls();
        Assert.Empty(fixture.Db.SupplierDeliveryNotes);
    }

    [Fact]
    public async Task ParsedLinesUseSupplierMappingAndDoNotGuessBetweenSizes()
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        var product = new Product(fixture.Supplier.Id, "SHIRT", "SHIRT", "Maglia", 0m, 22m, 10m);
        var small = new ProductVariant(product.Id, "SMALL", 10m, product.SupplierId) { Product = product };
        var large = new ProductVariant(product.Id, "LARGE", 10m, product.SupplierId) { Product = product };
        fixture.Db.AddRange(product, small, large);
        await fixture.Db.SaveChangesAsync();
        fixture.Db.SupplierArticleMappings.Add(new()
        {
            SupplierId = fixture.Supplier.Id, SupplierItemCode = "SUP-M", ArticleVariantId = small.Id
        });
        await fixture.Db.SaveChangesAsync();
        var note = await fixture.AddNoteAsync();
        var result = await fixture.Service.SaveLinesAsync(note.Id, new()
        {
            Version = note.Version, DocumentNumber = "B-1", DocumentDate = DateTimeOffset.UtcNow,
            Lines = [
                new() { SupplierItemCode = "SUP-M", Description = "Maglia M", Quantity = 5, UnitPrice = 12.34567890m },
                new() { SupplierItemCode = "SHIRT", Description = "Taglia sconosciuta", Quantity = 1, UnitPrice = 10m },
                new() { SupplierItemCode = "NEW", Description = "Nuovo articolo", Quantity = 2, UnitPrice = 20m }
            ]
        }, true, default);
        Assert.True(result.Success, result.Error);
        Assert.Equal("Parsed", result.Data!.Status);
        Assert.Equal(small.Id, result.Data.Lines[0].ArticleVariantId);
        Assert.Equal("Matched", result.Data.Lines[0].LineStatus);
        Assert.Equal("Maglia", result.Data.Lines[0].ArticleDescription);
        Assert.All(result.Data.Lines.Skip(1), l => Assert.Equal("NewArticleNeeded", l.LineStatus));
        Assert.Empty(fixture.Db.StockMovements);
    }

    [Fact]
    public async Task ParsedLinesMatchUniqueVariantSupplierCodesBeforeLegacyProductCodes()
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        var product = new Product(fixture.Supplier.Id, "SHIRT", "SHIRT", "Maglia", 0m, 22m, 10m);
        var small = new ProductVariant(product.Id, "SMALL", 10m, product.SupplierId, supplierCode: "SUP-M") { Product = product };
        var large = new ProductVariant(product.Id, "LARGE", 10m, product.SupplierId, supplierCode: "SUP-L") { Product = product };
        fixture.Db.AddRange(product, small, large);
        await fixture.Db.SaveChangesAsync();
        var note = await fixture.AddNoteAsync();

        var result = await fixture.Service.SaveLinesAsync(note.Id, new()
        {
            Version = note.Version, DocumentNumber = "B-2", DocumentDate = DateTimeOffset.UtcNow,
            Lines = [
                new() { SupplierItemCode = "SUP-L", Description = "Maglia L", Quantity = 1, UnitPrice = 10m },
                new() { SupplierItemCode = "SHIRT", Description = "Codice prodotto senza taglia", Quantity = 1, UnitPrice = 10m }
            ]
        }, true, default);

        Assert.True(result.Success, result.Error);
        Assert.Equal(large.Id, result.Data!.Lines[0].ArticleVariantId);
        Assert.Equal("Matched", result.Data.Lines[0].LineStatus);
        Assert.Equal("Maglia", result.Data.Lines[0].ArticleDescription);
        Assert.Null(result.Data.Lines[1].ArticleVariantId);
        Assert.Equal("NewArticleNeeded", result.Data.Lines[1].LineStatus);
    }

    [Theory]
    [InlineData("SUP-M", true)]
    [InlineData("SHIRT", false)]
    [InlineData("2900001444804", true)]
    [InlineData("2900001450614", true)]
    public async Task LookupMatchesSupplierVariantOrUniqueProductCodeWithoutSaving(string code, bool multipleVariants)
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        var product = new Product(fixture.Supplier.Id, "SHIRT", "SHIRT", "Maglia", 0m, 22m, 10m);
        var variant = new ProductVariant(product.Id, code.StartsWith("290", StringComparison.Ordinal) ? code : "SMALL",
            10m, product.SupplierId, supplierCode: "SUP-M")
        {
            Product = product
        };
        fixture.Db.AddRange(product, variant);
        if (multipleVariants)
            fixture.Db.Add(new ProductVariant(product.Id, "LARGE", 10m, product.SupplierId, supplierCode: "SUP-L"));
        var otherSupplier = new Contact("Altro fornitore con gli stessi codici");
        otherSupplier.EnableAsSupplier();
        fixture.Db.Add(otherSupplier);
        var otherProduct = new Product(otherSupplier.Id, "SHIRT", "OTHER-SHIRT", "Altra maglia", 0m, 22m, 10m);
        var otherVariant = new ProductVariant(otherProduct.Id, "OTHER-M", 10m, otherSupplier.Id, supplierCode: "SUP-M")
        {
            Product = otherProduct
        };
        fixture.Db.AddRange(otherSupplier, otherProduct, otherVariant);
        await fixture.Db.SaveChangesAsync();
        var note = await fixture.AddNoteAsync();
        var version = note.Version;

        var result = await fixture.Service.FindArticleAsync(note.Id, $" {code} ", default);

        Assert.True(result.Success, result.Error);
        Assert.Equal(variant.Id, result.Data!.ArticleVariantId);
        Assert.Equal("Maglia", result.Data.ArticleDescription);
        fixture.Db.ChangeTracker.Clear();
        var stored = await fixture.Db.SupplierDeliveryNotes.Include(x => x.Lines).SingleAsync(x => x.Id == note.Id);
        Assert.Equal(version, stored.Version);
        Assert.Equal(SupplierDeliveryNoteStatus.Draft, stored.Status);
        Assert.Empty(stored.Lines);
        Assert.Empty(fixture.Db.StockMovements);
        Assert.Empty(fixture.Db.SupplierArticleMappings);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SavingAndParsingMatchVariantBarcodesWithinTheSupplier(bool parsed)
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        var product = new Product(fixture.Supplier.Id, "SHIRT", "SHIRT", "Maglia", 0m, 22m, 10m);
        var small = new ProductVariant(product.Id, "2900001444804", 10m, product.SupplierId) { Product = product };
        var large = new ProductVariant(product.Id, "2900001450614", 10m, product.SupplierId) { Product = product };
        var otherSupplier = new Contact("Altro fornitore");
        otherSupplier.EnableAsSupplier();
        var otherProduct = new Product(otherSupplier.Id, "OTHER", "OTHER", "Altro articolo", 0m, 22m, 10m);
        var otherVariant = new ProductVariant(otherProduct.Id, "OTHER-BARCODE", 10m, otherSupplier.Id) { Product = otherProduct };
        fixture.Db.AddRange(product, small, large, otherSupplier, otherProduct, otherVariant);
        await fixture.Db.SaveChangesAsync();
        var note = await fixture.AddNoteAsync();

        var result = await fixture.Service.SaveLinesAsync(note.Id, new()
        {
            Version = note.Version, DocumentNumber = "BARCODES", DocumentDate = DateTimeOffset.UtcNow,
            Lines = [
                new() { SupplierItemCode = "2900001444804", Description = "Maglia M", Quantity = 1, UnitPrice = 10m },
                new() { SupplierItemCode = " 2900001450614 ", Description = "Maglia L", Quantity = 1, UnitPrice = 10m },
                new() { SupplierItemCode = "OTHER-BARCODE", Description = "Altro articolo", Quantity = 1, UnitPrice = 10m }
            ]
        }, parsed, default);

        Assert.True(result.Success, result.Error);
        Assert.Equal(small.Id, result.Data!.Lines[0].ArticleVariantId);
        Assert.Equal(large.Id, result.Data.Lines[1].ArticleVariantId);
        Assert.All(result.Data.Lines.Take(2), line => Assert.Equal("Matched", line.LineStatus));
        Assert.Null(result.Data.Lines[2].ArticleVariantId);
        Assert.Equal("NewArticleNeeded", result.Data.Lines[2].LineStatus);
        fixture.Db.ChangeTracker.Clear();
        var stored = await fixture.Db.SupplierDeliveryNotes.Include(x => x.Lines).SingleAsync(x => x.Id == note.Id);
        Assert.Equal(small.Id, stored.Lines.Single(x => x.Position == 0).ArticleVariantId);
        Assert.Equal(large.Id, stored.Lines.Single(x => x.Position == 1).ArticleVariantId);
    }

    [Fact]
    public async Task LookupDoesNotGuessAmbiguousCodesOrUseAnotherSuppliersArticles()
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        var product = new Product(fixture.Supplier.Id, "SHIRT", "SHIRT", "Maglia", 0m, 22m, 10m);
        var small = new ProductVariant(product.Id, "SMALL", 10m, product.SupplierId, supplierCode: "SUP-M") { Product = product };
        var large = new ProductVariant(product.Id, "LARGE", 10m, product.SupplierId, supplierCode: "SUP-L") { Product = product };
        var otherSupplier = new Contact("Altro fornitore");
        otherSupplier.EnableAsSupplier();
        fixture.Db.Add(otherSupplier);
        var otherProduct = new Product(otherSupplier.Id, "OTHER", "OTHER", "Altro articolo", 0m, 22m, 10m);
        var otherVariant = new ProductVariant(otherProduct.Id, "OTHER-M", 10m, otherSupplier.Id, supplierCode: "SUP-OTHER")
        {
            Product = otherProduct
        };
        fixture.Db.AddRange(product, small, large, otherSupplier, otherProduct, otherVariant);
        await fixture.Db.SaveChangesAsync();
        var note = await fixture.AddNoteAsync();

        foreach (var code in new[] { "SHIRT", "UNKNOWN", "OTHER", "SUP-OTHER", "OTHER-M", "" })
        {
            var result = await fixture.Service.FindArticleAsync(note.Id, code, default);
            Assert.False(result.Success);
            Assert.NotEmpty(result.Error!);
        }
        Assert.Empty(note.Lines);
        Assert.False(fixture.Db.ChangeTracker.HasChanges());
    }

    [Theory]
    [InlineData("variant")]
    [InlineData("mapping")]
    [InlineData("product")]
    public async Task LookupPreservesSupplierCodeAndMappingPrecedenceOverBarcodes(string source)
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        const string code = "2900001444804";
        var product = new Product(fixture.Supplier.Id, source == "product" ? code : "SHIRT",
            "SHIRT", "Maglia", 0m, 22m, 10m);
        var variant = new ProductVariant(product.Id, "SHIRT-M", 10m, product.SupplierId,
            supplierCode: source == "variant" ? code : null) { Product = product };
        var otherProduct = new Product(fixture.Supplier.Id, "OTHER", "OTHER", "Altro articolo", 0m, 22m, 10m);
        var barcodeVariant = new ProductVariant(otherProduct.Id, code, 10m, otherProduct.SupplierId) { Product = otherProduct };
        fixture.Db.AddRange(product, variant, otherProduct, barcodeVariant);
        if (source == "mapping")
            fixture.Db.SupplierArticleMappings.Add(new()
            {
                SupplierId = fixture.Supplier.Id, SupplierItemCode = code, ArticleVariantId = variant.Id
            });
        await fixture.Db.SaveChangesAsync();
        var note = await fixture.AddNoteAsync();

        var result = await fixture.Service.FindArticleAsync(note.Id, code, default);

        Assert.True(result.Success, result.Error);
        Assert.Equal(variant.Id, result.Data!.ArticleVariantId);
    }

    [Fact]
    public async Task LookupRejectsMissingOrNonEditableDocuments()
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        var missing = await fixture.Service.FindArticleAsync(Guid.NewGuid(), "SUP-M", default);
        Assert.False(missing.Success);
        Assert.Equal("Documento non trovato.", missing.Error);
        var note = await fixture.AddNoteAsync();
        note.Status = SupplierDeliveryNoteStatus.Confirmed;
        await fixture.Db.SaveChangesAsync();
        var confirmed = await fixture.Service.FindArticleAsync(note.Id, "SUP-M", default);
        Assert.False(confirmed.Success);
        Assert.StartsWith("Conflitto:", confirmed.Error);
    }

    [Fact]
    public async Task InvalidBatchLeavesHeaderAndExistingLinesUntouchedAndStaleEditsFail()
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        var note = await fixture.AddNoteAsync();
        var originalVersion = note.Version;
        var saved = await fixture.Service.SaveLinesAsync(note.Id, new()
        {
            Version = note.Version, DocumentNumber = "Original", DocumentDate = DateTimeOffset.UtcNow,
            Lines = [new() { Description = "Originale", Quantity = 2, UnitPrice = 1.12345678m }]
        }, false, default);
        Assert.True(saved.Success, saved.Error);
        var originalLine = Assert.Single(note.Lines);
        var invalid = await fixture.Service.SaveLinesAsync(note.Id, new()
        {
            Version = note.Version, DocumentNumber = "Changed", DocumentDate = DateTimeOffset.UtcNow,
            Lines = [
                new() { Description = "Valida", Quantity = 1, UnitPrice = 5m },
                new() { Description = "Non valida", Quantity = 0, UnitPrice = -1m }
            ]
        }, false, default);
        Assert.False(invalid.Success);
        Assert.Equal("Original", note.DocumentNumber);
        Assert.Same(originalLine, Assert.Single(note.Lines));
        Assert.False((await fixture.Service.CancelAsync(note.Id, originalVersion, default)).Success);
        Assert.True((await fixture.Service.CancelAsync(note.Id, note.Version, default)).Success);
        Assert.Equal(SupplierDeliveryNoteStatus.Cancelled, note.Status);
        Assert.False((await fixture.Service.SaveLinesAsync(note.Id, new()
        {
            Version = note.Version, DocumentDate = DateTimeOffset.UtcNow
        }, false, default)).Success);
    }

    [Fact]
    public async Task DocumentDatePreservesTheSupplierCalendarDayRegardlessOfOffset()
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        var note = await fixture.AddNoteAsync();
        var date = new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.FromHours(2));
        var result = await fixture.Service.SaveLinesAsync(note.Id, new()
        {
            Version = note.Version, DocumentNumber = "DATE", DocumentDate = date
        }, false, default);
        Assert.True(result.Success, result.Error);
        Assert.Equal(new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero), result.Data!.DocumentDate);
    }

    [Fact]
    public async Task ManualEditsAndRemovalsReplaceTheDraftLines()
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        var note = await fixture.AddNoteAsync();
        var request = new SaveSupplierDeliveryNoteLinesRequest
        {
            Version = note.Version, DocumentNumber = "Manual", DocumentDate = DateTimeOffset.UtcNow,
            Lines = [new() { Description = "Prima", Quantity = 1, UnitPrice = 10m },
                new() { Description = "Seconda", Quantity = 2, UnitPrice = 20m }]
        };
        Assert.True((await fixture.Service.SaveLinesAsync(note.Id, request, false, default)).Success);
        Assert.True((await fixture.Service.SaveLinesAsync(note.Id, request with
        {
            Version = note.Version, Lines = [new() { Description = "Modificata", Quantity = 3, UnitPrice = 30m }]
        }, false, default)).Success);
        fixture.Db.ChangeTracker.Clear();
        var reloaded = (await fixture.Service.GetAsync(note.Id, default))!;
        Assert.Equal("Modificata", Assert.Single(reloaded.Lines).Description);
        Assert.Single(fixture.Db.SupplierDeliveryNoteLines);
    }

    [Fact]
    public async Task CrossSupplierMappingAndTooPrecisePricesAreRejected()
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        var product = new Product(Guid.NewGuid(), "OTHER", "OTHER", "Altro", 0m, 22m, 10m);
        var variant = new ProductVariant(product.Id, "OTHER", 10m, product.SupplierId) { Product = product };
        fixture.Db.AddRange(product, variant);
        await fixture.Db.SaveChangesAsync();
        var note = await fixture.AddNoteAsync();
        var request = new SaveSupplierDeliveryNoteLinesRequest
        {
            Version = note.Version, DocumentDate = DateTimeOffset.UtcNow,
            Lines = [new() { ArticleVariantId = variant.Id, Description = "Altro fornitore", Quantity = 1, UnitPrice = 10m }]
        };
        Assert.False((await fixture.Service.SaveLinesAsync(note.Id, request, false, default)).Success);
        Assert.False((await fixture.Service.SaveLinesAsync(note.Id, request with
        {
            Lines = [new() { Description = "Precisione eccessiva", Quantity = 1, UnitPrice = 1.123456789m }]
        }, false, default)).Success);
        Assert.Empty(note.Lines);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public MauwDbContext Db { get; }
        public Contact Supplier { get; } = new("Fornitore test") { Status = ContactStatus.Active };
        public Warehouse Warehouse { get; private set; } = null!;
        public Mock<ISupplierDocumentStorage> Storage { get; } = new(MockBehavior.Strict);
        public SupplierDeliveryNoteService Service { get; }

        private Fixture(PostgreSqlContainerFixture postgres)
        {
            Db = postgres.CreateDbContext();
            Service = new(Db, Storage.Object, NullLogger<SupplierDeliveryNoteService>.Instance);
        }

        public static async Task<Fixture> CreateAsync(PostgreSqlContainerFixture postgres)
        {
            var fixture = new Fixture(postgres);
            fixture.Supplier.EnableAsSupplier();
            var address = fixture.Supplier.SetLegalHeadquarters("Via Test 1", "20100", "Milano");
            fixture.Db.Add(fixture.Supplier);
            await fixture.Db.SaveChangesAsync();
            fixture.Warehouse = new("TEST", "Magazzino test", WarehouseType.Standard, address.Id, true);
            fixture.Db.Add(fixture.Warehouse);
            await fixture.Db.SaveChangesAsync();
            fixture.Storage.Setup(x => x.UploadAsync(It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("supplier-delivery-notes/test/document.pdf");
            return fixture;
        }

        public async Task<SupplierDeliveryNote> AddNoteAsync()
        {
            var note = new SupplierDeliveryNote
            {
                SupplierId = Supplier.Id, WarehouseId = Warehouse.Id, DocumentDate = DateTime.UtcNow,
                FilePath = "test.pdf", ContentType = "application/pdf"
            };
            Db.Add(note);
            await Db.SaveChangesAsync();
            return note;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
