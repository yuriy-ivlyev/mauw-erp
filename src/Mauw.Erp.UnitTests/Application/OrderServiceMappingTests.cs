using Mauw.Erp.Application.Services;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Interfaces;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Domain.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

public sealed class OrderServiceMappingTests
{
    [Fact]
    public async Task GetAllOrdersPreservesFirstLinkedDocumentsAndEveryMatchingCreditNote()
    {
        var contact = new Contact("Cliente mapping");
        SetId(contact);
        var order = new Order(contact.Id, "ORD-012026-001") { Contact = contact };
        var orderWithoutDocuments = new Order(contact.Id, "ORD-012026-002") { Contact = contact };
        SetId(order);
        SetId(orderWithoutDocuments);
        var firstInvoice = new Invoice(contact.Id, "INV-1") { OrderId = order.Id };
        var laterInvoice = new Invoice(contact.Id, "INV-2") { OrderId = order.Id };
        var firstDdt = new DdtDocument(contact.Id, "DDT-1") { OrderId = order.Id };
        var laterDdt = new DdtDocument(contact.Id, "DDT-2") { OrderId = order.Id };
        SetId(firstInvoice);
        SetId(laterInvoice);
        SetId(firstDdt);
        SetId(laterDdt);
        var firstCreditNote = new CreditNote(firstInvoice.Id, "NC-1") { Reason = "Prima" };
        var secondCreditNote = new CreditNote(firstInvoice.Id, "NC-2") { Reason = "Seconda" };
        var otherCreditNote = new CreditNote(laterInvoice.Id, "NC-3") { Reason = "Altra fattura" };
        SetId(firstCreditNote);
        SetId(secondCreditNote);
        SetId(otherCreditNote);

        var unitOfWork = new Mock<IUnitOfWork>();
        var orders = new Mock<IOrderRepository>();
        var invoices = new Mock<IInvoiceRepository>();
        var ddts = new Mock<IDdtDocumentRepository>();
        var creditNotes = new Mock<ICreditNoteRepository>();
        unitOfWork.SetupGet(work => work.Orders).Returns(orders.Object);
        unitOfWork.SetupGet(work => work.Invoices).Returns(invoices.Object);
        unitOfWork.SetupGet(work => work.DdtDocuments).Returns(ddts.Object);
        unitOfWork.SetupGet(work => work.CreditNotes).Returns(creditNotes.Object);
        orders.Setup(repository => repository.GetAllPagedAsync(
                null, null, null, 1, 20, null, false))
            .ReturnsAsync((Enumerable.Repeat(order, 1).Append(orderWithoutDocuments), 2));
        invoices.Setup(repository => repository.GetByOrderIdsAsync(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync([firstInvoice, laterInvoice]);
        ddts.Setup(repository => repository.GetByOrderIdsAsync(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync([firstDdt, laterDdt]);
        creditNotes.Setup(repository => repository.GetByInvoiceIdsAsync(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync([firstCreditNote, secondCreditNote, otherCreditNote]);

        var service = new OrderService(
            unitOfWork.Object,
            new CreateOrderRequestValidator(),
            new CreateOrderItemRequestValidator(),
            new ItalianAccountingCalculator(),
            Mock.Of<ICompanySettingsService>(),
            Mock.Of<IInvoicePdfGenerator>(),
            NullLogger<OrderService>.Instance,
            Mock.Of<IDocumentPricingService>());

        var result = await service.GetAllOrdersAsync(null, null, null, 1, 20);

        Assert.True(result.Success, result.Error);
        var linked = Assert.Single(result.Data!.Items, item => item.Id == order.Id).LinkedDocuments!;
        Assert.Equal(firstInvoice.Id, linked.InvoiceId);
        Assert.Equal("INV-1", linked.InvoiceNumber);
        Assert.Equal(firstDdt.Id, linked.DdtId);
        Assert.Equal("DDT-1", linked.DdtNumber);
        Assert.Collection(linked.CreditNotes,
            first =>
            {
                Assert.Equal(firstCreditNote.Id, first.Id);
                Assert.Equal("NC-1", first.CreditNoteNumber);
                Assert.Equal("Prima", first.Reason);
            },
            second =>
            {
                Assert.Equal(secondCreditNote.Id, second.Id);
                Assert.Equal("NC-2", second.CreditNoteNumber);
                Assert.Equal("Seconda", second.Reason);
            });
        Assert.Null(Assert.Single(result.Data.Items, item => item.Id == orderWithoutDocuments.Id).LinkedDocuments);
    }

    private static void SetId(Entity<Guid> entity) =>
        typeof(Entity<Guid>).GetProperty(nameof(Entity<Guid>.Id))!.SetValue(entity, Guid.NewGuid());
}
