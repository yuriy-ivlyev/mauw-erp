using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Infrastructure.DbContext;
using Mauw.Erp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

[Collection(PostgreSqlTestCollection.Name)]
public class SupplierDeliveryNotePostgreSqlTests(PostgreSqlContainerFixture postgres)
{
    [Fact]
    public async Task ConfirmationPersistsIntegerStockExactPricesAndBidirectionalTraceabilityOnlyOnce()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedAsync(database);
        await using var db = database.CreateDbContext();
        var service = Service(db);
        var result = await service.ConfirmAsync(seed.NoteId, seed.Version, default);
        Assert.True(result.Success, result.Error);
        Assert.Equal("Confirmed", result.Data!.Status);
        Assert.All(result.Data.Lines, l => Assert.NotNull(l.StockMovementId));
        Assert.False((await service.ConfirmAsync(seed.NoteId, result.Data.Version, default)).Success);
        Assert.False((await service.CancelAsync(seed.NoteId, result.Data.Version, default)).Success);
        db.ChangeTracker.Clear();
        var note = await db.SupplierDeliveryNotes.Include(x => x.Lines).ThenInclude(x => x.StockMovement)
            .SingleAsync(x => x.Id == seed.NoteId);
        Assert.Equal(SupplierDeliveryNoteStatus.Confirmed, note.Status);
        var movements = await db.StockMovements.Where(x => x.ReferenceId == note.Id)
            .Include(x => x.SupplierDeliveryNoteLine).ToListAsync();
        Assert.Equal(2, movements.Count);
        Assert.All(movements, movement =>
        {
            Assert.Equal(StockMovementType.SupplierStockIn, movement.Type);
            Assert.Equal(StockMovementReason.SupplierReceipt, movement.Reason);
            Assert.Equal(seed.WarehouseId, movement.WarehouseId);
            Assert.Equal(12.34567890m, movement.UnitCost);
            Assert.Equal(note.DocumentDate, movement.MovementDateUtc);
            Assert.Equal(movement.SupplierDeliveryNoteLine!.Id, movement.SupplierDeliveryNoteLineId);
            Assert.Equal(movement.Id, movement.SupplierDeliveryNoteLine.StockMovement!.Id);
        });
        var stock = await db.StockItems.SingleAsync(x => x.WarehouseId == seed.WarehouseId);
        Assert.Equal(10m, stock.QuantityOnHand);
        Assert.Equal(12.34567890m, stock.UnitCost);
        Assert.Single(await db.SupplierArticleMappings.Where(x => x.SupplierId == seed.SupplierId).ToListAsync());

        var next = new SupplierDeliveryNote
        {
            SupplierId = seed.SupplierId, WarehouseId = seed.WarehouseId, DocumentDate = DateTime.UtcNow, FilePath = "next.pdf"
        };
        db.Add(next);
        await db.SaveChangesAsync();
        var mapped = await service.SaveLinesAsync(next.Id, new()
        {
            Version = next.Version, DocumentNumber = "Next", DocumentDate = DateTimeOffset.UtcNow,
            Lines = [new() { SupplierItemCode = "SUP-M", Description = "Maglia M", Quantity = 1, UnitPrice = 12m }]
        }, true, default);
        Assert.True(mapped.Success, mapped.Error);
        Assert.Equal(seed.VariantId, Assert.Single(mapped.Data!.Lines).ArticleVariantId);
    }

    [Fact]
    public async Task FailureAfterSaveRollsBackHeaderMovementsMappingAndStockTogether()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedAsync(database);
        await using (var db = database.CreateDbContext(new ThrowAfterSave()))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db).ConfirmAsync(seed.NoteId, seed.Version, default));
        }
        await using var verify = database.CreateDbContext();
        Assert.Equal(SupplierDeliveryNoteStatus.Draft,
            (await verify.SupplierDeliveryNotes.SingleAsync(x => x.Id == seed.NoteId)).Status);
        Assert.Equal(2m, (await verify.StockItems.SingleAsync(x => x.WarehouseId == seed.WarehouseId)).QuantityOnHand);
        Assert.Empty(await verify.StockMovements.Where(x => x.ReferenceId == seed.NoteId).ToListAsync());
        Assert.Empty(await verify.SupplierArticleMappings.Where(x => x.SupplierId == seed.SupplierId).ToListAsync());
    }

    [Fact]
    public async Task TwoConcurrentConfirmationsCreateExactlyOneSetOfMovements()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedAsync(database);
        var barrier = new ConfirmationBarrier();
        await using var first = database.CreateDbContext(barrier);
        await using var second = database.CreateDbContext(barrier);
        var results = await Task.WhenAll(
            Service(first).ConfirmAsync(seed.NoteId, seed.Version, default),
            Service(second).ConfirmAsync(seed.NoteId, seed.Version, default));
        Assert.Single(results, x => x.Success);
        Assert.Contains("Conflitto:", Assert.Single(results, x => !x.Success).Error);
        await using var verify = database.CreateDbContext();
        Assert.Equal(2, await verify.StockMovements.CountAsync(x => x.ReferenceId == seed.NoteId));
        Assert.Equal(10m, (await verify.StockItems.SingleAsync(x => x.WarehouseId == seed.WarehouseId)).QuantityOnHand);
    }

    [Fact]
    public async Task ConfirmationCreatesMissingStockWithoutDuplicatingRowsForTheSameVariant()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedAsync(database);
        await using var db = database.CreateDbContext();
        db.StockItems.Remove(await db.StockItems.SingleAsync(x => x.WarehouseId == seed.WarehouseId));
        await db.SaveChangesAsync();
        Assert.True((await Service(db).ConfirmAsync(seed.NoteId, seed.Version, default)).Success);
        db.ChangeTracker.Clear();
        Assert.Equal(8m, (await db.StockItems.SingleAsync(x => x.WarehouseId == seed.WarehouseId)).QuantityOnHand);
    }

    [Fact]
    public async Task ConcurrentDocumentsCannotLoseStockAndTheRejectedDocumentCanBeRetried()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedAsync(database);
        Guid otherId;
        Guid otherVersion;
        await using (var setup = database.CreateDbContext())
        {
            var secondNote = new SupplierDeliveryNote
            {
                SupplierId = seed.SupplierId, WarehouseId = seed.WarehouseId, DocumentNumber = "DDT-SECOND",
                DocumentDate = DateTime.UtcNow, FilePath = "second.pdf",
                Lines = [new()
                {
                    ArticleVariantId = seed.VariantId, SupplierItemCode = "SUP-M", Description = "Maglia M",
                    Quantity = 8, UnitPrice = 12.34567890m, LineStatus = SupplierDeliveryNoteLineStatus.Matched
                }]
            };
            setup.Add(secondNote);
            await setup.SaveChangesAsync();
            otherId = secondNote.Id; otherVersion = secondNote.Version;
        }
        var barrier = new ConfirmationBarrier();
        await using var first = database.CreateDbContext(barrier);
        await using var second = database.CreateDbContext(barrier);
        var results = await Task.WhenAll(
            Service(first).ConfirmAsync(seed.NoteId, seed.Version, default),
            Service(second).ConfirmAsync(otherId, otherVersion, default));
        Assert.Single(results, x => x.Success);
        await using var retry = database.CreateDbContext();
        var rejectedId = results[0].Success ? otherId : seed.NoteId;
        var rejectedVersion = results[0].Success ? otherVersion : seed.Version;
        Assert.True((await Service(retry).ConfirmAsync(rejectedId, rejectedVersion, default)).Success);
        Assert.Equal(18m, (await retry.StockItems.SingleAsync(x => x.WarehouseId == seed.WarehouseId)).QuantityOnHand);
        Assert.Equal(3, await retry.StockMovements.CountAsync(x => x.ReferenceId == seed.NoteId || x.ReferenceId == otherId));
    }

    [Fact]
    public async Task UnmatchedOrOverflowingDocumentsNeverMutateAnyStock()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedAsync(database);
        await using var db = database.CreateDbContext();
        var note = await db.SupplierDeliveryNotes.Include(x => x.Lines).SingleAsync(x => x.Id == seed.NoteId);
        note.Lines.Last().ArticleVariantId = null;
        await db.SaveChangesAsync();
        var service = Service(db);
        Assert.False((await service.ConfirmAsync(note.Id, note.Version, default)).Success);
        note.Lines.Last().ArticleVariantId = seed.VariantId;
        note.Lines.Last().Quantity = int.MaxValue;
        await db.SaveChangesAsync();
        Assert.False((await service.ConfirmAsync(note.Id, note.Version, default)).Success);
        Assert.Equal(SupplierDeliveryNoteStatus.Draft, note.Status);
        Assert.Equal(2m, (await db.StockItems.SingleAsync(x => x.WarehouseId == seed.WarehouseId)).QuantityOnHand);
        Assert.Empty(await db.StockMovements.Where(x => x.ReferenceId == seed.NoteId).ToListAsync());
    }

    [Fact]
    public async Task DatabaseUniqueConstraintPreventsMoreThanOneMovementForTheSameLine()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedAsync(database);
        await using var db = database.CreateDbContext();
        Assert.True((await Service(db).ConfirmAsync(seed.NoteId, seed.Version, default)).Success);
        db.ChangeTracker.Clear();
        var note = await db.SupplierDeliveryNotes.Include(x => x.Lines).SingleAsync(x => x.Id == seed.NoteId);
        db.StockMovements.Add(StockMovement.FromSupplierDeliveryNote(note, note.Lines.First()));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private static SupplierDeliveryNoteService Service(MauwDbContext db) =>
        new(db, Mock.Of<ISupplierDocumentStorage>(), NullLogger<SupplierDeliveryNoteService>.Instance);

    private static async Task<Seed> SeedAsync(PostgreSqlTestDatabase database)
    {
        await using var db = database.CreateDbContext();
        if (!await db.Countries.AnyAsync(x => x.IsoCode == "IT")) db.Countries.Add(new() { IsoCode = "IT", Name = "Italia" });
        var supplier = new Contact("Fornitore DDT test") { Status = ContactStatus.Active };
        supplier.EnableAsSupplier();
        db.Add(supplier);
        var address = new ContactAddress(supplier.Id, AddressPurpose.LegalHeadquarters, "Via Test 1", "20100", "Milano");
        db.Add(address);
        var code = Guid.NewGuid().ToString("N");
        var warehouse = new Warehouse(code[..16], "DDT test", WarehouseType.Standard, address.Id, true);
        db.Add(warehouse);
        var product = new Product(supplier.Id, code, code, "Maglia M", 22m, 12m);
        db.Add(product);
        var variant = new ProductVariant(product.Id, code, 12m, product.SupplierId) { Product = product };
        db.Add(variant);
        await db.SaveChangesAsync();
        db.StockItems.Add(new(warehouse.Id, variant.Id, 2m, 10m));
        var note = new SupplierDeliveryNote
        {
            SupplierId = supplier.Id, WarehouseId = warehouse.Id, DocumentNumber = "DDT-TEST",
            DocumentDate = new(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc),
            FilePath = "test.pdf", ContentType = "application/pdf"
        };
        foreach (var quantity in new[] { 5, 3 })
            note.Lines.Add(new()
            {
                SupplierDeliveryNoteId = note.Id, ArticleVariantId = variant.Id, Position = note.Lines.Count,
                SupplierItemCode = "SUP-M", Description = "Maglia M", Quantity = quantity,
                UnitPrice = 12.34567890m, LineStatus = SupplierDeliveryNoteLineStatus.Matched
            });
        db.Add(note);
        await db.SaveChangesAsync();
        return new(note.Id, note.Version, supplier.Id, warehouse.Id, variant.Id);
    }

    private sealed record Seed(Guid NoteId, Guid Version, Guid SupplierId, Guid WarehouseId, Guid VariantId);

    private sealed class ThrowAfterSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Injected failure after SaveChanges, before commit.");
    }

    private sealed class ConfirmationBarrier : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _count;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _count) == 2) _ready.TrySetResult();
            await _ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            return result;
        }
    }
}
