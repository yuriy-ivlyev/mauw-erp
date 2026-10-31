using Mauw.Erp.Application.Commands.Orders;
using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Dtos.Pdf;
using Mauw.Erp.Contracts.Enums;
using Mauw.Erp.Contracts.Interfaces;
using Mauw.Erp.Domain.Accounting;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Pricing;
using Mauw.Erp.Domain.Services;
using Mauw.Erp.Infrastructure.DbContext;
using Mauw.Erp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

[Collection(PostgreSqlTestCollection.Name)]
public sealed class OrderPreSdiAdjustmentPostgreSqlTests(PostgreSqlContainerFixture postgres)
{
    private const string ReopenNote = "Ordine riaperto per variazione pre-trasmissione SDI";

    [Theory]
    [InlineData("Pending")]
    [InlineData("Transmitted")]
    [InlineData("Accepted")]
    [InlineData("LegacySubmissionTimestamp")]
    public async Task OrderCannotBeReopenedOnceTheInvoiceIsQueuedOrSentToSdi(string state)
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedCompletedCashOrderWithInvoiceAsync(database);
        await SetInvoiceSdiStateAsync(database, seed.InvoiceId, state);

        await using var db = database.CreateDbContext();
        var result = await Harness.Create(db, seed).Reopen.Handle(
            new ReopenOrderForAdjustmentCommand(seed.OrderId), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("SDI", result.Error);
        await using var verify = database.CreateDbContext();
        Assert.False((await verify.Orders.SingleAsync()).IsUnderAdjustment);
        Assert.Empty(await verify.DocumentAuditEntries.ToListAsync());
        var dto = (await Harness.Create(verify, seed).Orders.GetOrderAsync(seed.OrderId)).Data!;
        Assert.True(dto.IsReadOnly);
        Assert.Empty(dto.PermittedActions);
    }

    [Fact]
    public async Task OrderWithoutInvoiceAndNotCompletedOrderCannotBeReopened()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedCompletedCashOrderWithInvoiceAsync(database);
        Guid draftOrderId;
        await using (var setup = database.CreateDbContext())
        {
            var draft = new Order(seed.ContactId, "ORD-DRAFT", fulfillmentType: OrderFulfillmentType.CashDesk);
            setup.Add(draft);
            await setup.SaveChangesAsync();
            draftOrderId = draft.Id;
            setup.Invoices.RemoveRange(setup.Invoices);
            await setup.SaveChangesAsync();
        }

        await using var db = database.CreateDbContext();
        var harness = Harness.Create(db, seed);
        var noInvoice = await harness.Reopen.Handle(new(seed.OrderId), CancellationToken.None);
        var notCompleted = await harness.Reopen.Handle(new(draftOrderId), CancellationToken.None);
        var missing = await harness.Reopen.Handle(new(Guid.NewGuid()), CancellationToken.None);

        Assert.False(noInvoice.Success);
        Assert.False(notCompleted.Success);
        Assert.False(missing.Success);
        Assert.Contains("not found", missing.Error);
    }

    [Fact]
    public async Task ReopeningRecordsTheAuditNoteAndUnlocksOnlyTheLines()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedCompletedCashOrderWithInvoiceAsync(database);

        await using (var db = database.CreateDbContext())
        {
            var result = await Harness.Create(db, seed).Reopen.Handle(
                new ReopenOrderForAdjustmentCommand(seed.OrderId), CancellationToken.None);

            Assert.True(result.Success, result.Error);
            Assert.True(result.Data!.IsUnderAdjustment);
            Assert.False(result.Data.IsReadOnly);
            Assert.False(result.Data.CanEditInvoice);
            Assert.Equal(OrderStatusEnum.Completed, result.Data.Status);
            Assert.Equal("NotTransmitted", result.Data.InvoiceSdiStatus);
            var action = Assert.Single(result.Data.PermittedActions);
            Assert.Equal("SyncAndFinalizeInvoice", action.Action);

            var again = await Harness.Create(db, seed).Reopen.Handle(
                new ReopenOrderForAdjustmentCommand(seed.OrderId), CancellationToken.None);
            Assert.False(again.Success);
        }

        await using var verify = database.CreateDbContext();
        var order = await verify.Orders.SingleAsync();
        Assert.True(order.IsUnderAdjustment);
        Assert.Equal("Mario Rossi", order.AdjustmentStartedBy);
        var audit = Assert.Single(await verify.DocumentAuditEntries.ToListAsync());
        Assert.Equal(AuditDocumentTypes.Order, audit.DocumentType);
        Assert.Equal(seed.OrderId, audit.DocumentId);
        Assert.Equal(ReopenNote, audit.Note);
        Assert.Equal("user-1", audit.UserId);
        Assert.Equal("Mario Rossi", audit.UserName);
        Assert.InRange(audit.OccurredAtUtc, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(1));
        Assert.Empty(await verify.StockMovements.ToListAsync());
    }

    [Fact]
    public async Task LinesCannotBeEditedUntilTheOrderIsReopened()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedCompletedCashOrderWithInvoiceAsync(database);

        await using var db = database.CreateDbContext();
        var result = await Harness.Create(db, seed).Update.Handle(
            new UpdateOrderAdjustmentLinesCommand(seed.OrderId, [new(seed.Line22Id, null, 5)]),
            CancellationToken.None);

        Assert.False(result.Success);
        await using var verify = database.CreateDbContext();
        Assert.Equal(2, (await verify.OrderItems.SingleAsync(item => item.Id == seed.Line22Id)).Quantity);
        Assert.Empty(await verify.StockMovements.ToListAsync());
    }

    [Fact]
    public async Task QuantityIncreaseDischargesTheDifferenceAndDecreaseReturnsItToStock()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedCompletedCashOrderWithInvoiceAsync(database);
        await ReopenAsync(database, seed);

        // 2 -> 5 pieces of the 22% line: a further discharge of 3.
        var increased = await UpdateAsync(database, seed, new OrderAdjustmentLine(seed.Line22Id, null, 5),
            new OrderAdjustmentLine(seed.Line4Id, null, 1));
        Assert.True(increased.Success, increased.Error);
        Assert.Equal(5m, await OnHandAsync(database, seed.Variant22Id));
        Assert.Equal(4m, await OnHandAsync(database, seed.Variant4Id));

        // 5 -> 1 pieces: 4 pieces come back.
        var decreased = await UpdateAsync(database, seed, new OrderAdjustmentLine(seed.Line22Id, null, 1),
            new OrderAdjustmentLine(seed.Line4Id, null, 1));
        Assert.True(decreased.Success, decreased.Error);
        Assert.Equal(9m, await OnHandAsync(database, seed.Variant22Id));

        // Removing the 4% line returns its piece; adding a new variant discharges it.
        var replaced = await UpdateAsync(database, seed, new OrderAdjustmentLine(seed.Line22Id, null, 1),
            new OrderAdjustmentLine(null, seed.VariantNewId, 2));
        Assert.True(replaced.Success, replaced.Error);
        Assert.Equal(5m, await OnHandAsync(database, seed.Variant4Id));
        Assert.Equal(8m, await OnHandAsync(database, seed.VariantNewId));

        await using var verify = database.CreateDbContext();
        var movements = await verify.StockMovements.OrderBy(movement => movement.CreatedAt).ToListAsync();
        Assert.All(movements, movement =>
        {
            Assert.Equal(StockMovementReason.OrderAdjustment, movement.Reason);
            Assert.Equal(seed.OrderId, movement.ReferenceId);
            Assert.Equal(seed.WarehouseId, movement.WarehouseId);
            Assert.Equal("user-1", movement.CreatedByUserId);
        });
        Assert.Equal(
            [-3m, -2m, 1m, 4m],
            movements.Select(movement => movement.QuantityDelta!.Value).Order().ToArray());
        Assert.All(movements, movement => Assert.Equal(
            movement.QuantityDelta < 0 ? StockMovementType.SalesDelivery : StockMovementType.CustomerReturn,
            movement.Type));
        Assert.Equal(3, await verify.DocumentAuditEntries.CountAsync(entry => entry.Action == AuditActions.OrderLinesAdjusted));
        var order = await verify.Orders.Include(item => item.Items).SingleAsync();
        Assert.Equal(2, order.Items.Count);
        Assert.Equal(1, order.Items.Single(item => item.VariantId == seed.Variant22Id).Quantity);
        Assert.Equal(2, order.Items.Single(item => item.VariantId == seed.VariantNewId).Quantity);
        Assert.Equal(30m, order.TaxableTotal);
        Assert.Equal(4.2m, order.VatTotal);
        Assert.Equal(34.2m, order.GrossTotal);
    }

    [Fact]
    public async Task InsufficientStockForTheDischargeLeavesLinesStockAndAuditUntouched()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedCompletedCashOrderWithInvoiceAsync(database);
        await ReopenAsync(database, seed);

        var result = await UpdateAsync(database, seed, new OrderAdjustmentLine(seed.Line22Id, null, 11),
            new OrderAdjustmentLine(seed.Line4Id, null, 1));

        Assert.False(result.Success);
        Assert.Contains("Giacenza insufficiente", result.Error);
        await using var verify = database.CreateDbContext();
        Assert.Equal(2, (await verify.OrderItems.SingleAsync(item => item.Id == seed.Line22Id)).Quantity);
        Assert.Equal(8m, await OnHandAsync(database, seed.Variant22Id));
        Assert.Empty(await verify.StockMovements.ToListAsync());
        Assert.Equal(1, await verify.DocumentAuditEntries.CountAsync());
        var order = await verify.Orders.SingleAsync();
        Assert.Equal((120m, 8.4m, 128.4m), (order.TaxableTotal, order.VatTotal, order.GrossTotal));
    }

    [Fact]
    public async Task SyncOverwritesTheExistingInvoiceKeepingItsIdAndNumberAndClosesTheAdjustment()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedCompletedCashOrderWithInvoiceAsync(database);
        await ReopenAsync(database, seed);
        var updated = await UpdateAsync(database, seed, new OrderAdjustmentLine(seed.Line22Id, null, 5),
            new OrderAdjustmentLine(seed.Line4Id, null, 2));
        Assert.True(updated.Success, updated.Error);
        InvoicePdfModel? renderedModel = null;

        await using (var db = database.CreateDbContext())
        {
            var harness = Harness.Create(db, seed);
            harness.Pdf.Setup(generator => generator.GenerateInvoicePdfAsync(
                    It.IsAny<InvoicePdfModel>(), It.IsAny<CancellationToken>()))
                .Callback<InvoicePdfModel, CancellationToken>((model, _) => renderedModel = model)
                .ReturnsAsync([1, 2, 3]);

            var result = await harness.Sync.Handle(new SyncAndFinalizeInvoiceCommand(seed.OrderId), CancellationToken.None);

            Assert.True(result.Success, result.Error);
            Assert.False(result.Data!.IsUnderAdjustment);
            Assert.True(result.Data.IsReadOnly);
            Assert.Equal(OrderStatusEnum.Completed, result.Data.Status);
            Assert.Equal(seed.InvoiceId, result.Data.LinkedDocuments!.InvoiceId);
            Assert.Equal(seed.InvoiceNumber, result.Data.LinkedDocuments.InvoiceNumber);
            Assert.Equal(269m, result.Data.GrossTotal);
        }

        await using var verify = database.CreateDbContext();
        var invoice = await verify.Invoices.Include(item => item.Items).SingleAsync();
        Assert.Equal(seed.InvoiceId, invoice.Id);
        Assert.Equal(seed.InvoiceNumber, invoice.InvoiceNumber);
        Assert.Equal(seed.InvoiceConfirmedAt, invoice.ConfirmedAt);
        Assert.Equal(SdiStatus.NotTransmitted, invoice.SdiStatus);
        Assert.Equal(2, invoice.Items.Count);
        Assert.Equal(250m, invoice.TaxableTotal);
        Assert.Equal(19m, invoice.VatTotal);
        Assert.Equal(269m, invoice.GrossTotal);
        var order = await verify.Orders.SingleAsync();
        Assert.False(order.IsUnderAdjustment);
        Assert.Null(order.AdjustmentStartedAt);
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal((order.TaxableTotal, order.VatTotal, order.GrossTotal),
            (invoice.TaxableTotal, invoice.VatTotal, invoice.GrossTotal));

        Assert.NotNull(renderedModel);
        Assert.Equal(seed.InvoiceNumber, renderedModel!.DocumentNumber);
        Assert.Equal(269m, renderedModel.Totals.TotalGross);
        Assert.Collection(renderedModel.VatSummary.OrderBy(row => row.VatRate),
            row => Assert.Equal((4m, 200m, 8m), (row.VatRate, row.TaxableAmount, row.VatAmount)),
            row => Assert.Equal((22m, 50m, 11m), (row.VatRate, row.TaxableAmount, row.VatAmount)));

        var audit = await verify.DocumentAuditEntries.SingleAsync(entry => entry.DocumentType == AuditDocumentTypes.Invoice);
        Assert.Equal(seed.InvoiceId, audit.DocumentId);
        Assert.Equal(AuditActions.InvoiceSynchronizedFromOrder, audit.Action);
        Assert.Equal("user-1", audit.UserId);
        Assert.Equal("Mario Rossi", audit.UserName);
        Assert.Contains("totale 128.40 -> 269.00", audit.Details);
        Assert.InRange(audit.OccurredAtUtc, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(1));

        await using var read = database.CreateDbContext();
        var xml = await Harness.Create(read, seed).Invoices.GetInvoiceXmlAsync(seed.InvoiceId);
        Assert.True(xml.Success, xml.Error);
        Assert.Contains("<ImportoTotaleDocumento>269.00</ImportoTotaleDocumento>", xml.Data);
        Assert.Contains(seed.InvoiceNumber, xml.Data);
        Assert.Empty(FatturaPaSchemaValidator.Validate(xml.Data!));
        var dto = (await Harness.Create(read, seed).Invoices.GetInvoiceAsync(seed.InvoiceId)).Data!;
        Assert.False(dto.IsOrderUnderAdjustment);
        Assert.Equal("NotTransmitted", dto.SdiStatus);

        var closed = await UpdateAsync(database, seed, new OrderAdjustmentLine(seed.Line22Id, null, 1));
        Assert.False(closed.Success);
    }

    [Fact]
    public async Task ForeignCustomerInvoiceIsSynchronizedAndExportedWithForeignIdentification()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedCompletedCashOrderWithInvoiceAsync(database);
        await using (var setup = database.CreateDbContext())
        {
            var customer = await setup.Contacts.Include(contact => contact.Addresses).SingleAsync();
            customer.CountryCode = "DE";
            customer.VatNumber = "DE123456789";
            customer.SdiCode = "KRRH6B9";
            customer.PecEmail = "kunde@example.test";
            var address = customer.Addresses.Single();
            address.CountryCode = "DE";
            address.City = "Berlin";
            address.ZipCode = "10115";
            await setup.SaveChangesAsync();
        }

        await ReopenAsync(database, seed);
        var updated = await UpdateAsync(database, seed, new OrderAdjustmentLine(seed.Line22Id, null, 5),
            new OrderAdjustmentLine(seed.Line4Id, null, 2));
        Assert.True(updated.Success, updated.Error);
        await using (var db = database.CreateDbContext())
        {
            var synced = await Harness.Create(db, seed).Sync.Handle(
                new SyncAndFinalizeInvoiceCommand(seed.OrderId), CancellationToken.None);
            Assert.True(synced.Success, synced.Error);
        }

        await using var read = database.CreateDbContext();
        var xml = await Harness.Create(read, seed).Invoices.GetInvoiceXmlAsync(seed.InvoiceId);
        Assert.True(xml.Success, xml.Error);
        Assert.Empty(FatturaPaSchemaValidator.Validate(xml.Data!));
        var document = System.Xml.Linq.XDocument.Parse(xml.Data!);
        var recipient = document.Descendants("CessionarioCommittente").Single();
        Assert.Equal("DE", recipient.Descendants("IdPaese").Single().Value);
        Assert.Equal("123456789", recipient.Descendants("IdCodice").Single().Value);
        Assert.Equal("DE", recipient.Descendants("Nazione").Single().Value);
        Assert.Equal("XXXXXXX", document.Descendants("CodiceDestinatario").Single().Value);
        Assert.Empty(document.Descendants("PECDestinatario"));
    }

    [Fact]
    public async Task FailureWhileRegeneratingTheXmlRollsBackInvoiceAndOrderTogether()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedCompletedCashOrderWithInvoiceAsync(database);
        await ReopenAsync(database, seed);
        var updated = await UpdateAsync(database, seed, new OrderAdjustmentLine(seed.Line22Id, null, 5),
            new OrderAdjustmentLine(seed.Line4Id, null, 2));
        Assert.True(updated.Success, updated.Error);
        await using (var setup = database.CreateDbContext())
        {
            var address = await setup.ContactAddresses.SingleAsync();
            address.ZipCode = "XX";
            await setup.SaveChangesAsync();
        }

        await using (var db = database.CreateDbContext())
        {
            var result = await Harness.Create(db, seed).Sync.Handle(
                new SyncAndFinalizeInvoiceCommand(seed.OrderId), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("XML", result.Error);
        }

        await using var verify = database.CreateDbContext();
        var invoice = await verify.Invoices.Include(item => item.Items).SingleAsync();
        Assert.Equal(seed.InvoiceId, invoice.Id);
        Assert.Equal(128.4m, invoice.GrossTotal);
        Assert.Equal(2, invoice.Items.Count);
        Assert.Equal([2m, 1m], invoice.Items.OrderByDescending(item => item.Quantity).Select(item => item.Quantity).ToArray());
        Assert.True((await verify.Orders.SingleAsync()).IsUnderAdjustment);
        Assert.Empty(await verify.DocumentAuditEntries.Where(entry => entry.DocumentType == AuditDocumentTypes.Invoice).ToListAsync());
    }

    [Fact]
    public async Task InvoiceTransmittedAfterReopeningBlocksFurtherEditsAndSynchronization()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedCompletedCashOrderWithInvoiceAsync(database);
        await ReopenAsync(database, seed);
        await SetInvoiceSdiStateAsync(database, seed.InvoiceId, "Transmitted");

        var update = await UpdateAsync(database, seed, new OrderAdjustmentLine(seed.Line22Id, null, 5),
            new OrderAdjustmentLine(seed.Line4Id, null, 1));
        await using var db = database.CreateDbContext();
        var sync = await Harness.Create(db, seed).Sync.Handle(new SyncAndFinalizeInvoiceCommand(seed.OrderId), CancellationToken.None);
        var dto = (await Harness.Create(db, seed).Orders.GetOrderAsync(seed.OrderId)).Data!;

        Assert.False(update.Success);
        Assert.False(sync.Success);
        Assert.True(dto.IsReadOnly);
        Assert.Empty(dto.PermittedActions);
        await using var verify = database.CreateDbContext();
        Assert.Equal(128.4m, (await verify.Invoices.SingleAsync()).GrossTotal);
        Assert.Empty(await verify.StockMovements.ToListAsync());
    }

    [Fact]
    public async Task LegacyCompletedOrderEditsAreRejectedWhileTheOrderIsUnderAdjustment()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedCompletedCashOrderWithInvoiceAsync(database);
        await ReopenAsync(database, seed);

        await using var db = database.CreateDbContext();
        var orders = Harness.Create(db, seed).Orders;
        var shipping = await orders.UpdateCompletedInvoiceShippingChargeAsync(
            seed.OrderId, new(10m, 22m, null));
        var billing = await orders.AddCompletedBillingItemAsync(seed.OrderId, new(seed.Variant22Id, 1, null));
        var removal = await orders.RemoveCompletedBillingItemAsync(seed.OrderId, seed.Line22Id);

        Assert.All([shipping, billing, removal], result =>
        {
            Assert.False(result.Success);
            Assert.Contains("rettifica", result.Error, StringComparison.OrdinalIgnoreCase);
        });
        await using var verify = database.CreateDbContext();
        Assert.Equal(128.4m, (await verify.Invoices.SingleAsync()).GrossTotal);
    }

    [Fact]
    public async Task ShippingOrderWithResidualUnshippedQuantityIsNotReopened()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedCompletedShippingOrderWithInvoiceAsync(database, shippedQuantity: 1, orderedQuantity: 3);

        await using var db = database.CreateDbContext();
        var result = await Harness.Create(db, seed).Reopen.Handle(new(seed.OrderId), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("non interamente spedite", result.Error);
        await using var verify = database.CreateDbContext();
        Assert.False((await verify.Orders.SingleAsync()).IsUnderAdjustment);
    }

    [Fact]
    public async Task FullyShippedOrderPostsTheDeltaInItsFulfillmentWarehouse()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedCompletedShippingOrderWithInvoiceAsync(database, shippedQuantity: 3, orderedQuantity: 3);
        await ReopenAsync(database, seed);

        var increased = await UpdateAsync(database, seed, new OrderAdjustmentLine(seed.Line22Id, null, 4));
        var decreased = await UpdateAsync(database, seed, new OrderAdjustmentLine(seed.Line22Id, null, 2));

        Assert.True(increased.Success, increased.Error);
        Assert.True(decreased.Success, decreased.Error);
        await using var verify = database.CreateDbContext();
        var line = await verify.OrderItems.SingleAsync();
        Assert.Equal(2, line.Quantity);
        Assert.Equal(2, line.QuantityShipped);
        Assert.Equal(9m, await OnHandAsync(database, seed.Variant22Id));
        var movements = await verify.StockMovements.OrderBy(movement => movement.CreatedAt).ToListAsync();
        Assert.Equal([-1m, 2m], movements.Select(movement => movement.QuantityDelta!.Value).ToArray());
        Assert.All(movements, movement => Assert.Equal(seed.WarehouseId, movement.WarehouseId));
    }

    private static async Task ReopenAsync(PostgreSqlTestDatabase database, Seed seed)
    {
        await using var db = database.CreateDbContext();
        var result = await Harness.Create(db, seed).Reopen.Handle(new(seed.OrderId), CancellationToken.None);
        Assert.True(result.Success, result.Error);
    }

    private static async Task<OperationResult<OrderDto>> UpdateAsync(
        PostgreSqlTestDatabase database, Seed seed, params OrderAdjustmentLine[] lines)
    {
        await using var db = database.CreateDbContext();
        return await Harness.Create(db, seed).Update.Handle(
            new UpdateOrderAdjustmentLinesCommand(seed.OrderId, lines), CancellationToken.None);
    }

    private static async Task<decimal> OnHandAsync(PostgreSqlTestDatabase database, Guid variantId)
    {
        await using var db = database.CreateDbContext();
        return (await db.StockItems.SingleAsync(stock => stock.ProductVariantId == variantId)).QuantityOnHand;
    }

    private static async Task SetInvoiceSdiStateAsync(PostgreSqlTestDatabase database, Guid invoiceId, string state)
    {
        await using var db = database.CreateDbContext();
        var invoice = await db.Invoices.SingleAsync(item => item.Id == invoiceId);
        switch (state)
        {
            case "Pending":
                Assert.True(invoice.MarkSdiPending().Success);
                break;
            case "Transmitted":
                Assert.True(invoice.MarkSdiTransmitted(DateTime.UtcNow).Success);
                break;
            case "Accepted":
                Assert.True(invoice.MarkSdiTransmitted(DateTime.UtcNow).Success);
                Assert.True(invoice.MarkSdiAccepted().Success);
                break;
            default:
                db.Entry(invoice).Property(value => value.SdiSubmittedAt).CurrentValue = DateTime.UtcNow;
                break;
        }

        await db.SaveChangesAsync();
    }

    private async Task<Seed> SeedCompletedCashOrderWithInvoiceAsync(PostgreSqlTestDatabase database)
    {
        var seed = await SeedCatalogAsync(database);
        await using (var setup = database.CreateDbContext())
        {
            var calculator = new ItalianAccountingCalculator();
            var order = new Order(seed.ContactId, "ORD-CASH-1", fulfillmentType: OrderFulfillmentType.CashDesk);
            Assert.True(order.AddItem(new OrderItem(order.Id, seed.Variant22Id, 2, 10m, 22m), calculator).Success);
            Assert.True(order.AddItem(new OrderItem(order.Id, seed.Variant4Id, 1, 100m, 4m), calculator).Success);
            setup.Add(order);
            setup.Entry(order).Property(value => value.Status).CurrentValue = OrderStatus.Completed;
            await setup.SaveChangesAsync();
            seed = seed with
            {
                OrderId = order.Id,
                Line22Id = order.Items.Single(item => item.VariantId == seed.Variant22Id).Id,
                Line4Id = order.Items.Single(item => item.VariantId == seed.Variant4Id).Id
            };
        }

        return await GenerateInvoiceAsync(database, seed);
    }

    private async Task<Seed> SeedCompletedShippingOrderWithInvoiceAsync(
        PostgreSqlTestDatabase database, int shippedQuantity, int orderedQuantity)
    {
        var seed = await SeedCatalogAsync(database);
        await using (var setup = database.CreateDbContext())
        {
            var calculator = new ItalianAccountingCalculator();
            var order = new Order(seed.ContactId, "ORD-SHIP-1");
            order.SetFulfillmentWarehouse(seed.WarehouseId);
            var item = new OrderItem(order.Id, seed.Variant22Id, orderedQuantity, 10m, 22m);
            Assert.True(order.AddItem(item, calculator).Success);
            item.RecordShipment(shippedQuantity);
            setup.Add(order);
            setup.Entry(order).Property(value => value.Status).CurrentValue = OrderStatus.Completed;
            await setup.SaveChangesAsync();
            seed = seed with { OrderId = order.Id, Line22Id = item.Id };
        }

        return await GenerateInvoiceAsync(database, seed);
    }

    private static async Task<Seed> GenerateInvoiceAsync(PostgreSqlTestDatabase database, Seed seed)
    {
        await using var db = database.CreateDbContext();
        var generated = await Harness.Create(db, seed).Invoices.GenerateFromOrderAsync(seed.OrderId);
        Assert.True(generated.Success, generated.Error);
        var invoice = await db.Invoices.AsNoTracking().SingleAsync(item => item.Id == generated.Data!.Id);
        return seed with
        {
            InvoiceId = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            InvoiceConfirmedAt = invoice.ConfirmedAt
        };
    }

    private static async Task<Seed> SeedCatalogAsync(PostgreSqlTestDatabase database)
    {
        await using var db = database.CreateDbContext();
        if (!await db.Countries.AnyAsync(country => country.IsoCode == "IT"))
            db.Countries.Add(new() { IsoCode = "IT", Name = "Italia" });

        var contact = new Contact("Cliente Prova S.r.l.", "09876543210") { Status = ContactStatus.Active };
        contact.EnableAsCustomer();
        contact.EnableAsSupplier();
        db.Add(contact);
        var address = new ContactAddress(
            contact.Id, AddressPurpose.LegalHeadquarters, "Via Test 1", "20100", "Milano", "MI");
        db.Add(address);
        var code = Guid.NewGuid().ToString("N");
        var warehouse = new Warehouse(
            code[..16], "Magazzino rettifiche", WarehouseType.Standard, address.Id,
            isSellable: true, isDefault: true);
        db.Add(warehouse);
        var variant22 = NewVariant(db, contact, "22", 22m);
        var variant4 = NewVariant(db, contact, "4", 4m);
        var variantNew = NewVariant(db, contact, "NEW", 10m);
        await db.SaveChangesAsync();
        // Stock after the original sale of 2 + 1 pieces (initial 10 and 5).
        db.StockItems.Add(new(warehouse.Id, variant22.Id, 8m, 5m));
        db.StockItems.Add(new(warehouse.Id, variant4.Id, 4m, 5m));
        db.StockItems.Add(new(warehouse.Id, variantNew.Id, 10m, 5m));
        await db.SaveChangesAsync();
        return new Seed(
            contact.Id, warehouse.Id, variant22.Id, variant4.Id, variantNew.Id,
            Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, string.Empty, null);
    }

    private static ProductVariant NewVariant(MauwDbContext db, Contact supplier, string label, decimal vatRate)
    {
        var code = $"{label}-{Guid.NewGuid():N}";
        var product = new Product(supplier.Id, code, code, $"Articolo {label}", vatRate, 12m);
        db.Add(product);
        var variant = new ProductVariant(product.Id, code, 12m, supplier.Id) { Product = product };
        db.Add(variant);
        return variant;
    }

    private sealed record Seed(
        Guid ContactId,
        Guid WarehouseId,
        Guid Variant22Id,
        Guid Variant4Id,
        Guid VariantNewId,
        Guid OrderId,
        Guid Line22Id,
        Guid Line4Id,
        Guid InvoiceId,
        string InvoiceNumber,
        DateTime? InvoiceConfirmedAt);

    private sealed class Harness
    {
        public required Mock<IInvoicePdfGenerator> Pdf { get; init; }
        public required IOrderService Orders { get; init; }
        public required InvoiceService Invoices { get; init; }
        public required ReopenOrderForAdjustmentCommandHandler Reopen { get; init; }
        public required UpdateOrderAdjustmentLinesCommandHandler Update { get; init; }
        public required SyncAndFinalizeInvoiceCommandHandler Sync { get; init; }

        public static Harness Create(MauwDbContext db, Seed seed)
        {
            var calculator = new ItalianAccountingCalculator();
            var work = new UnitOfWork(db, new MemoryCache(new MemoryCacheOptions()));
            var pdf = new Mock<IInvoicePdfGenerator>();
            pdf.Setup(generator => generator.GenerateInvoicePdfAsync(
                    It.IsAny<InvoicePdfModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([1, 2, 3]);
            var company = new Mock<ICompanySettingsService>();
            company.Setup(service => service.GetCompanyAsync())
                .ReturnsAsync(OperationResult<CompanySettingsDto>.Ok(Company()));
            var pricing = new FakePricing(calculator, new Dictionary<Guid, (decimal Price, decimal VatRate)>
            {
                [seed.Variant22Id] = (10m, 22m),
                [seed.Variant4Id] = (100m, 4m),
                [seed.VariantNewId] = (10m, 10m)
            });
            var user = new Mock<ICurrentUserService>();
            user.SetupGet(service => service.UserId).Returns("user-1");
            user.SetupGet(service => service.FullName).Returns("Mario Rossi");
            user.SetupGet(service => service.Email).Returns("mario@example.test");
            var orders = new OrderService(
                work, new CreateOrderRequestValidator(), new CreateOrderItemRequestValidator(), calculator,
                company.Object, pdf.Object, NullLogger<OrderService>.Instance, pricing);
            var invoices = new InvoiceService(work, calculator, company.Object, pdf.Object);
            return new Harness
            {
                Pdf = pdf,
                Orders = orders,
                Invoices = invoices,
                Reopen = new ReopenOrderForAdjustmentCommandHandler(work, orders, user.Object),
                Update = new UpdateOrderAdjustmentLinesCommandHandler(work, orders, pricing, calculator, user.Object),
                Sync = new SyncAndFinalizeInvoiceCommandHandler(work, orders, invoices, calculator, user.Object)
            };
        }

        private static CompanySettingsDto Company() => new(
            Guid.NewGuid(), "Mauw S.r.l.", "IT01234567890", "01234567890", "KRRH6B9", "mauw@pec.example.test",
            "IT", "RF01 - Ordinario", "591450", "BO", 10000m, true, false, null,
            new CompanyAddressSiteDto(Guid.NewGuid(), "LegalHeadquarters", null, "Via Roma 1", "40100", "Bologna",
                "BO", "IT", false, null, null, null, null, null, null, null),
            [], IsShareCapitalFullyPaid: true, BankName: "Banca Prova", Iban: "IT60X0542811101000000123456",
            BicSwift: "BPITITMM");
    }

    private sealed class FakePricing(
        ItalianAccountingCalculator calculator,
        IReadOnlyDictionary<Guid, (decimal Price, decimal VatRate)> catalog) : IDocumentPricingService
    {
        public Task<AccountingResult<PreparedDocumentPricing>> PrepareAsync(
            Guid contactId, IReadOnlyList<DocumentPricingRequest> lines, DateTimeOffset at,
            CancellationToken ct = default)
        {
            var inputs = lines.Select(line => new DocumentLineInput(
                line.Quantity, catalog[line.VariantId].Price, line.VatRate ?? catalog[line.VariantId].VatRate,
                line.NatureCode, line.DiscountPercent ?? 0m)).ToList();
            var document = calculator.Calculate(inputs);
            return Task.FromResult(document.Success
                ? AccountingResult<PreparedDocumentPricing>.Ok(new(
                    document.Data!.Lines
                        .Select((line, index) => new PreparedPricingLine(
                            line, new PricingSnapshot(null, null, line.UnitNetPrice, line.UnitNetPrice, false, null, false, null)))
                        .ToList(),
                    document.Data))
                : AccountingResult<PreparedDocumentPricing>.Fail(document.Error));
        }
    }
}
