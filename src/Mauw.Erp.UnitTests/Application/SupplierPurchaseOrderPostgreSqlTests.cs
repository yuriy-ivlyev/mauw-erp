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
public sealed class SupplierPurchaseOrderPostgreSqlTests(PostgreSqlContainerFixture postgres)
{
    [Fact]
    public async Task SupplierOrderReceiptReconciliationAndReturnDraftPreservePhysicalStock()
    {
        var database = postgres.CreateDatabase();
        Guid supplierId;
        Guid warehouseId;
        Guid variantId;
        Guid secondVariantId;
        await using (var seed = database.CreateDbContext())
        {
            if (!await seed.Countries.AnyAsync(x => x.IsoCode == "IT"))
                seed.Countries.Add(new() { IsoCode = "IT", Name = "Italia" });
            var supplier = new Contact("Fornitore ordini test") { Status = ContactStatus.Active };
            supplier.EnableAsSupplier();
            seed.Add(supplier);
            var address = new ContactAddress(supplier.Id, AddressPurpose.LegalHeadquarters,
                "Via Test 1", "20100", "Milano");
            seed.Add(address);
            var code = Guid.NewGuid().ToString("N");
            var warehouse = new Warehouse(code[..16], "Ordini test", WarehouseType.Standard, address.Id, true);
            seed.Add(warehouse);
            var product = new Product(supplier.Id, code, code, "Giacca test", 22m, 10m);
            seed.Add(product);
            var variant = new ProductVariant(product.Id, code, 10m, product.SupplierId) { Product = product };
            seed.Add(variant);
            var secondVariant = new ProductVariant(product.Id, $"{code}-2", 10m, product.SupplierId) { Product = product };
            seed.Add(secondVariant);
            await seed.SaveChangesAsync();
            supplierId = supplier.Id;
            warehouseId = warehouse.Id;
            variantId = variant.Id;
            secondVariantId = secondVariant.Id;
        }

        Guid orderId;
        Guid orderLineId;
        Guid secondOrderLineId;
        Guid orderVersion;
        await using (var db = database.CreateDbContext())
        {
            var service = new SupplierPurchaseOrderService(db, NullLogger<SupplierPurchaseOrderService>.Instance);
            var created = await service.CreateAsync(new()
            {
                SupplierId = supplierId,
                OrderDate = DateTimeOffset.UtcNow,
                Lines =
                [
                    new() { ArticleVariantId = variantId, QuantityOrdered = 5, UnitCost = 10.12345678m },
                    new() { ArticleVariantId = secondVariantId, QuantityOrdered = 9, UnitCost = 11m }
                ]
            }, default);
            Assert.True(created.Success, created.Error);
            orderId = created.Data!.Id;
            orderLineId = created.Data.Lines.Single(x => x.ArticleVariantId == variantId).Id;
            secondOrderLineId = created.Data.Lines.Single(x => x.ArticleVariantId == secondVariantId).Id;
            var confirmed = await service.ConfirmAsync(orderId, created.Data.Version, default);
            Assert.True(confirmed.Success, confirmed.Error);
            orderVersion = confirmed.Data!.Version;
            Assert.NotEqual(created.Data.Version, orderVersion);
        }

        Guid noteId;
        Guid noteVersion;
        await using (var db = database.CreateDbContext())
        {
            var note = new SupplierDeliveryNote
            {
                SupplierId = supplierId,
                SupplierPurchaseOrderId = orderId,
                WarehouseId = warehouseId,
                DocumentNumber = "DDT-ORDINE-1",
                DocumentDate = DateTime.UtcNow,
                FilePath = "test.pdf",
                ContentType = "application/pdf",
                Lines =
                [
                    new()
                    {
                        SupplierPurchaseOrderLineId = orderLineId,
                        ArticleVariantId = variantId,
                        SupplierItemCode = "SKU-TEST",
                        Description = "Giacca test",
                        Quantity = 7,
                        UnitPrice = 10.12345678m,
                        LineStatus = SupplierDeliveryNoteLineStatus.Matched
                    },
                    new()
                    {
                        SupplierPurchaseOrderLineId = secondOrderLineId,
                        ArticleVariantId = secondVariantId,
                        SupplierItemCode = "SKU-TEST-2",
                        Description = "Giacca test seconda variante",
                        Quantity = 7,
                        UnitPrice = 11m,
                        LineStatus = SupplierDeliveryNoteLineStatus.Matched
                    }
                ]
            };
            db.Add(note);
            await db.SaveChangesAsync();
            noteId = note.Id;
            noteVersion = note.Version;
        }

        await using (var db = database.CreateDbContext())
        {
            var storage = new Mock<ISupplierDocumentStorage>();
            var service = new SupplierDeliveryNoteService(db, storage.Object,
                NullLogger<SupplierDeliveryNoteService>.Instance);
            var receipt = await service.ConfirmAsync(noteId, noteVersion, default);
            Assert.True(receipt.Success, receipt.Error);
        }

        await using (var db = database.CreateDbContext())
        {
            Assert.Equal(7m, (await db.StockItems.SingleAsync(x =>
                x.WarehouseId == warehouseId && x.ProductVariantId == variantId)).QuantityOnHand);
            Assert.Equal(7m, (await db.StockItems.SingleAsync(x =>
                x.WarehouseId == warehouseId && x.ProductVariantId == secondVariantId)).QuantityOnHand);
            Assert.Equal(2, await db.StockMovements.CountAsync(x => x.ReferenceId == noteId));

            var orders = new SupplierPurchaseOrderService(db, NullLogger<SupplierPurchaseOrderService>.Instance);
            var order = await orders.GetAsync(orderId, default);
            Assert.Equal("PartiallyReceived", order!.Status);
            Assert.Equal(7, order.Lines.Single(x => x.ArticleVariantId == variantId).QuantityReceived);
            Assert.Equal(7, order.Lines.Single(x => x.ArticleVariantId == secondVariantId).QuantityReceived);
            var reconciliation = await orders.ReconcileAsync(orderId, default);
            var variance = reconciliation!.Lines.Single(x => x.ArticleVariantId == variantId);
            Assert.Equal(2, variance.Delta);
            Assert.Equal("Surplus", variance.Status);
            var shortage = reconciliation.Lines.Single(x => x.ArticleVariantId == secondVariantId);
            Assert.Equal(-2, shortage.Delta);
            Assert.Equal("Shortage", shortage.Status);
            var shortages = await orders.ShortagesAsync(orderId, default);
            Assert.Equal(2, Assert.Single(shortages!).QuantityMissing);

            var returned = await orders.CreateReturnDraftAsync(orderId,
                new([new(variance.Receipts.Single().SupplierDeliveryNoteLineId, 2)]), default);
            Assert.True(returned.Success, returned.Error);
            Assert.Equal(2, Assert.Single(returned.Data!.Lines).Quantity);
            Assert.Equal(7m, (await db.StockItems.SingleAsync(x =>
                x.WarehouseId == warehouseId && x.ProductVariantId == variantId)).QuantityOnHand);

            var duplicate = await orders.CreateReturnDraftAsync(orderId,
                new([new(variance.Receipts.Single().SupplierDeliveryNoteLineId, 1)]), default);
            Assert.False(duplicate.Success);
            Assert.NotEqual(orderVersion, (await orders.GetAsync(orderId, default))!.Version);

            var issued = await orders.IssueReturnDraftAsync(orderId, returned.Data.Id, returned.Data.Version, default);
            Assert.True(issued.Success, issued.Error);
            Assert.Equal("Issued", issued.Data!.Status);
            Assert.Equal(5m, (await db.StockItems.SingleAsync(x =>
                x.WarehouseId == warehouseId && x.ProductVariantId == variantId)).QuantityOnHand);
            Assert.Equal(7m, (await db.StockItems.SingleAsync(x =>
                x.WarehouseId == warehouseId && x.ProductVariantId == secondVariantId)).QuantityOnHand);
            Assert.Single(await db.StockMovements.Where(x => x.ReferenceId == returned.Data.Id
                && x.Type == StockMovementType.SupplierStockReturn).ToListAsync());
            var afterReturn = await orders.GetAsync(orderId, default);
            Assert.Equal("PartiallyReceived", afterReturn!.Status);
            Assert.Equal(5, afterReturn.Lines.Single(x => x.ArticleVariantId == variantId).QuantityReceived);
            Assert.Equal("InLine", (await orders.ReconcileAsync(orderId, default))!.Lines
                .Single(x => x.ArticleVariantId == variantId).Status);
            Assert.Equal("Shortage", (await orders.ReconcileAsync(orderId, default))!.Lines
                .Single(x => x.ArticleVariantId == secondVariantId).Status);
        }
    }
}
