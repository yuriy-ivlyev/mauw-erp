using System.Text;
using Mauw.Erp.Application.Commands.Invoices;
using Mauw.Erp.Application.Commands.Orders;
using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Dtos.Pdf;
using Mauw.Erp.Contracts.Interfaces;
using Mauw.Erp.Domain.Accounting;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Domain.Services;
using Mauw.Erp.Infrastructure.DbContext;
using Mauw.Erp.Infrastructure.Repositories;
using Mauw.Erp.Infrastructure.Storage;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

[Collection(PostgreSqlTestCollection.Name)]
public sealed class InvoiceSdiTransmissionPostgreSqlTests(PostgreSqlContainerFixture postgres)
{
    [Fact]
    public async Task TransmissionArchivesXmlRecordsAuditAndPreventsOrderAdjustment()
    {
        var database = postgres.CreateDatabase();
        var seed = await CreateInvoiceAsync(database);
        var xml = CreateValidXml(seed.InvoiceNumber);
        var xmlBytes = Encoding.UTF8.GetBytes(xml);
        var storageKey = "invoices/2026/10/IT01234567890_0261000001.xml";
        var storage = CreateStorage(storageKey, xmlBytes);
        var provider = new Mock<ISdiTransmissionProvider>(MockBehavior.Strict);
        provider.Setup(value => value.TransmitAsync(
                It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SdiTransmissionResult(true, "SDI-OUT-001", null));
        var invoiceXml = new Mock<IInvoiceService>(MockBehavior.Strict);
        invoiceXml.Setup(value => value.GetInvoiceXmlAsync(seed.InvoiceId))
            .ReturnsAsync(OperationResult<string>.Ok(xml));
        var user = CreateUser();

        await using (var db = database.CreateDbContext())
        {
            var handler = new TransmitInvoiceToSdiCommandHandler(
                new UnitOfWork(db, new MemoryCache(new MemoryCacheOptions())),
                invoiceXml.Object,
                storage.Object,
                provider.Object,
                user.Object);

            var result = await handler.Handle(
                new TransmitInvoiceToSdiCommand(seed.InvoiceId), CancellationToken.None);

            Assert.True(result.Success, result.Error);
            Assert.Equal("Transmitted", result.Data!.SdiStatus);
        }

        var expectedFileName = $"IT01234567890_{ReadProgressive(xml)}.xml";
        storage.Verify(value => value.UploadInvoiceXmlAsync(
            expectedFileName, It.Is<byte[]>(content => content.SequenceEqual(xmlBytes)),
            It.IsAny<CancellationToken>()), Times.Once);
        provider.Verify(value => value.TransmitAsync(
            expectedFileName, It.Is<byte[]>(content => content.SequenceEqual(xmlBytes)),
            It.IsAny<CancellationToken>()), Times.Once);
        storage.Verify(value => value.DeleteInvoiceXmlAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        await using (var verify = database.CreateDbContext())
        {
            var invoice = await verify.Invoices.SingleAsync(value => value.Id == seed.InvoiceId);
            Assert.Equal(expectedFileName, invoice.SdiFileName);
            Assert.Equal(storageKey, invoice.SdiStorageKey);
            Assert.Equal("SDI-OUT-001", invoice.SdiTransmissionId);
            Assert.Equal(SdiStatus.Transmitted, invoice.SdiStatus);
            Assert.NotNull(invoice.SdiTransmittedAtUtc);

            var audit = Assert.Single(await verify.DocumentAuditEntries.ToListAsync());
            Assert.Equal(AuditDocumentTypes.Invoice, audit.DocumentType);
            Assert.Equal(seed.InvoiceId, audit.DocumentId);
            Assert.Equal(AuditActions.InvoiceTransmittedToSdi, audit.Action);
            Assert.Equal(
                $"Fattura trasmessa a SDI con file {expectedFileName} e ID SDI-OUT-001",
                audit.Note);
            Assert.Equal("user-1", audit.UserId);
        }

        await using (var db = database.CreateDbContext())
        {
            var cache = new MemoryCache(new MemoryCacheOptions());
            var work = new UnitOfWork(db, cache);
            var reopen = new ReopenOrderForAdjustmentCommandHandler(
                work, Mock.Of<IOrderService>(), user.Object);

            var result = await reopen.Handle(
                new ReopenOrderForAdjustmentCommand(seed.OrderId), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("SDI", result.Error);
            Assert.False((await db.Orders.SingleAsync()).IsUnderAdjustment);
        }

        await using (var db = database.CreateDbContext())
        {
            var downloadService = new InvoiceService(
                new UnitOfWork(db, new MemoryCache(new MemoryCacheOptions())),
                new ItalianAccountingCalculator(),
                Mock.Of<ICompanySettingsService>(),
                Mock.Of<IInvoicePdfGenerator>(),
                storage.Object);

            var download = await downloadService.GetInvoiceXmlDownloadAsync(seed.InvoiceId);

            Assert.True(download.Success, download.Error);
            Assert.Equal(expectedFileName, download.Data!.FileName);
            Assert.Equal(xmlBytes, download.Data.Content);
        }

        storage.Verify(value => value.GetInvoiceXmlAsync(storageKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FailedProviderTransmissionDeletesUploadedXmlAndKeepsInvoiceUntransmitted()
    {
        var database = postgres.CreateDatabase();
        var seed = await CreateInvoiceAsync(database);
        var xml = CreateValidXml(seed.InvoiceNumber);
        var storageKey = "invoices/2026/10/failed-invoice.xml";
        var storage = CreateStorage(storageKey, Encoding.UTF8.GetBytes(xml));
        var provider = new Mock<ISdiTransmissionProvider>(MockBehavior.Strict);
        provider.Setup(value => value.TransmitAsync(
                It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SdiTransmissionResult(false, null, "Provider SDI non disponibile."));
        var invoiceXml = new Mock<IInvoiceService>(MockBehavior.Strict);
        invoiceXml.Setup(value => value.GetInvoiceXmlAsync(seed.InvoiceId))
            .ReturnsAsync(OperationResult<string>.Ok(xml));

        await using (var db = database.CreateDbContext())
        {
            var handler = new TransmitInvoiceToSdiCommandHandler(
                new UnitOfWork(db, new MemoryCache(new MemoryCacheOptions())),
                invoiceXml.Object,
                storage.Object,
                provider.Object,
                CreateUser().Object);

            var result = await handler.Handle(
                new TransmitInvoiceToSdiCommand(seed.InvoiceId), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("Provider SDI non disponibile.", result.Error);
        }

        storage.Verify(value => value.DeleteInvoiceXmlAsync(storageKey, It.IsAny<CancellationToken>()), Times.Once);
        await using var verify = database.CreateDbContext();
        var invoice = await verify.Invoices.SingleAsync(value => value.Id == seed.InvoiceId);
        Assert.Equal(SdiStatus.NotTransmitted, invoice.SdiStatus);
        Assert.Null(invoice.SdiStorageKey);
        Assert.Empty(await verify.DocumentAuditEntries.ToListAsync());
    }

    [Fact]
    public async Task BulkTransmissionProcessesEachInvoiceAndSkipsOrdersUnderAdjustment()
    {
        var database = postgres.CreateDatabase();
        var eligible = await CreateInvoiceAsync(database);
        var underAdjustment = await CreateInvoiceAsync(database);
        await using (var setup = database.CreateDbContext())
        {
            var order = await setup.Orders.SingleAsync(value => value.Id == underAdjustment.OrderId);
            Assert.True(order.BeginAdjustment("Mario Rossi").Success);
            await setup.SaveChangesAsync();
        }

        var eligibleXml = CreateValidXml(eligible.InvoiceNumber);
        var adjustmentXml = CreateValidXml(underAdjustment.InvoiceNumber);
        var invoiceXml = new Mock<IInvoiceService>(MockBehavior.Strict);
        invoiceXml.Setup(value => value.GetInvoiceXmlAsync(eligible.InvoiceId))
            .ReturnsAsync(OperationResult<string>.Ok(eligibleXml));
        invoiceXml.Setup(value => value.GetInvoiceXmlAsync(underAdjustment.InvoiceId))
            .ReturnsAsync(OperationResult<string>.Ok(adjustmentXml));
        var storage = new Mock<ISdiDocumentStorage>(MockBehavior.Strict);
        storage.Setup(value => value.UploadInvoiceXmlAsync(
                It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string fileName, byte[] _, CancellationToken _) =>
                $"invoices/2026/10/{fileName}");
        storage.Setup(value => value.DeleteInvoiceXmlAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var provider = new Mock<ISdiTransmissionProvider>(MockBehavior.Strict);
        provider.Setup(value => value.TransmitAsync(
                It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SdiTransmissionResult(true, "SDI-BULK-001", null));
        var user = CreateUser();

        await using (var listDb = database.CreateDbContext())
        {
            var listService = new InvoiceService(
                new UnitOfWork(listDb, new MemoryCache(new MemoryCacheOptions())),
                new ItalianAccountingCalculator(),
                Mock.Of<ICompanySettingsService>(),
                Mock.Of<IInvoicePdfGenerator>(),
                storage.Object);
            var list = await listService.GetInvoicesAsync(
                null, null, 1, 20, sdiStatus: nameof(SdiStatus.NotTransmitted));

            Assert.True(list.Success, list.Error);
            Assert.Equal(2, list.Data!.Items.Count);
            Assert.True(list.Data.Items.Single(value => value.Id == eligible.InvoiceId).CanTransmitToSdi);
            Assert.False(list.Data.Items.Single(value => value.Id == underAdjustment.InvoiceId).CanTransmitToSdi);
        }

        await using (var db = database.CreateDbContext())
        {
            var unitOfWork = new UnitOfWork(db, new MemoryCache(new MemoryCacheOptions()));
            var transmissionHandler = new TransmitInvoiceToSdiCommandHandler(
                unitOfWork, invoiceXml.Object, storage.Object, provider.Object, user.Object);
            var sender = new Mock<ISender>(MockBehavior.Strict);
            sender.Setup(value => value.Send(
                    It.IsAny<IRequest<OperationResult<InvoiceDto>>>(), It.IsAny<CancellationToken>()))
                .Returns((IRequest<OperationResult<InvoiceDto>> request, CancellationToken token) =>
                    transmissionHandler.Handle((TransmitInvoiceToSdiCommand)request, token));
            var bulkHandler = new BulkTransmitInvoicesToSdiCommandHandler(
                sender.Object, NullLogger<BulkTransmitInvoicesToSdiCommandHandler>.Instance);

            var result = await bulkHandler.Handle(
                new BulkTransmitInvoicesToSdiCommand([eligible.InvoiceId, underAdjustment.InvoiceId]),
                CancellationToken.None);

            Assert.True(result.Success, result.Error);
            Assert.Equal(2, result.Data!.RequestedCount);
            Assert.Equal(1, result.Data.SuccessCount);
            Assert.Equal(1, result.Data.FailedCount);
            Assert.True(result.Data.Results[0].Success);
            Assert.Contains("rettifica", result.Data.Results[1].ErrorMessage);
        }

        storage.Verify(value => value.UploadInvoiceXmlAsync(
            It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        provider.Verify(value => value.TransmitAsync(
            It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        await using var verify = database.CreateDbContext();
        Assert.Equal(SdiStatus.Transmitted,
            (await verify.Invoices.SingleAsync(value => value.Id == eligible.InvoiceId)).SdiStatus);
        Assert.Equal(SdiStatus.NotTransmitted,
            (await verify.Invoices.SingleAsync(value => value.Id == underAdjustment.InvoiceId)).SdiStatus);
        Assert.Single(await verify.DocumentAuditEntries.ToListAsync());
    }

    private static async Task<InvoiceSeed> CreateInvoiceAsync(PostgreSqlTestDatabase database)
    {
        var calculator = new ItalianAccountingCalculator();
        Guid orderId;
        await using (var setup = database.CreateDbContext())
        {
            var contact = new Contact("Cliente SDI");
            var product = new Product(
                Guid.NewGuid(), "FORN", $"SDI-{Guid.NewGuid():N}", "Articolo SDI", 22m, 10m);
            var variant = new ProductVariant(
                product.Id, $"SDI-BC-{Guid.NewGuid():N}", 5m, product.SupplierId)
            {
                Product = product
            };
            setup.AddRange(contact, product, variant);
            await setup.SaveChangesAsync();

            var order = new Order(contact.Id, $"ORD-SDI-{Guid.NewGuid():N}",
                fulfillmentType: OrderFulfillmentType.CashDesk)
            {
                Contact = contact
            };
            Assert.True(order.AddItem(
                new OrderItem(order.Id, variant.Id, 1, 10m, 22m), calculator).Success);
            setup.Add(order);
            setup.Entry(order).Property(value => value.Status).CurrentValue = OrderStatus.Completed;
            await setup.SaveChangesAsync();
            orderId = order.Id;
        }

        await using var db = database.CreateDbContext();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var invoiceService = new InvoiceService(
            new UnitOfWork(db, cache),
            calculator,
            Mock.Of<ICompanySettingsService>(),
            Mock.Of<IInvoicePdfGenerator>(),
            Mock.Of<ISdiDocumentStorage>());
        var generated = await invoiceService.GenerateFromOrderAsync(orderId);
        Assert.True(generated.Success, generated.Error);
        return new InvoiceSeed(orderId, generated.Data!.Id, generated.Data.InvoiceNumber);
    }

    private static Mock<ISdiDocumentStorage> CreateStorage(string key, byte[] content)
    {
        var storage = new Mock<ISdiDocumentStorage>(MockBehavior.Strict);
        storage.Setup(value => value.UploadInvoiceXmlAsync(
                It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(key);
        storage.Setup(value => value.GetInvoiceXmlAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);
        storage.Setup(value => value.DeleteInvoiceXmlAsync(key, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return storage;
    }

    private static Mock<ICurrentUserService> CreateUser()
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(value => value.UserId).Returns("user-1");
        user.SetupGet(value => value.FullName).Returns("Mario Rossi");
        user.SetupGet(value => value.Email).Returns("mario@example.test");
        return user;
    }

    private static string CreateValidXml(string invoiceNumber)
    {
        var issueDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var model = new InvoicePdfModel(
            DocumentNumber: invoiceNumber,
            DocumentType: "TD01",
            IssueDate: issueDate,
            Currency: "EUR",
            Seller: new InvoicePdfSellerDetails(
                "Mauw S.r.l.", "IT01234567890", "01234567890", "Via Roma 1", "Bologna", "40100", "BO",
                "BO-591450", "10.000,00", "RF01 - Ordinario", "KRRH6B9", "mauw@pec.example.test",
                IsShareCapitalFullyPaid: true, ReaOffice: "BO", IsSoleShareholder: true, IsInLiquidation: false),
            Customer: new InvoicePdfCustomerDetails(
                "Cliente Prova S.r.l.", "IT09876543210", null, "Via Milano 2", "Milano", "20100", "MI",
                "0000000", "cliente@pec.example.test"),
            Payment: new InvoicePdfPaymentInfo(
                "A vista", "MP01", issueDate, "IT60X0542811101000000123456", "BPITITMM", "Banca Prova", 12.20m),
            Lines: [new InvoicePdfLineModel(
                "SKU-1", "Articolo SDI", null, 1m, 10m, null, 22m, 10m, null)],
            VatSummary: [new InvoiceVatBreakdownModel(22m, 10m, 2.20m, null)],
            Totals: new InvoicePdfTotals(1m, 10m, 2.20m, 12.20m));
        var result = FatturaPaInvoiceXmlBuilder.Build(model, new ItalianAccountingCalculator());
        Assert.True(result.Success, result.Error);
        Assert.Empty(FatturaPaSchemaValidator.Validate(result.Data!));
        return result.Data!;
    }

    private static string ReadProgressive(string xml)
    {
        var document = System.Xml.Linq.XDocument.Parse(xml);
        return document.Descendants("ProgressivoInvio").Single().Value;
    }

    private sealed record InvoiceSeed(Guid OrderId, Guid InvoiceId, string InvoiceNumber);
}
