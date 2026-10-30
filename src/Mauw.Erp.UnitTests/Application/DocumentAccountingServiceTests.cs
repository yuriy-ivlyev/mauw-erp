using Mauw.Erp.Application.Services;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Contracts.Interfaces;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Domain.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

public class DocumentAccountingServiceTests
{
    private readonly Fixture _fixture = new();

    [Fact]
    public async Task InvalidCartIsRejectedBeforeCreatingOrSavingAnOrder()
    {
        var result = await _fixture.OrderService.CreateCassaSessionAsync(new(_fixture.Contact.Id, null, null,
            [new(_fixture.Variant.Id, 1, 10m, 22m, 10), new(_fixture.Variant.Id, 1, 10m, 0m, 10)]));

        Assert.False(result.Success);
        _fixture.UnitOfWork.Verify(work => work.Orders.AddWithNextNumberAsync(
            It.IsAny<Order>(), It.IsAny<string>()), Times.Never);
        _fixture.UnitOfWork.Verify(work => work.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task InvalidCartUpdatePreservesExistingOrder()
    {
        var order = new Order(_fixture.Contact.Id, "ORD-TEST");
        Assert.True(order.AddItem(new(order.Id, _fixture.Variant.Id, 1, 10m, 22m), _fixture.Calculator).Success);
        _fixture.UnitOfWork.Setup(work => work.Orders.GetCassaSessionWithItemsAsync(order.Id)).ReturnsAsync(order);

        var result = await _fixture.OrderService.UpdateCassaSessionAsync(order.Id,
            new(_fixture.Contact.Id, null, null, [new(_fixture.Variant.Id, 1, 10m, 0m, 10)]));

        Assert.False(result.Success);
        Assert.Equal(10m, Assert.Single(order.Items).LineNetTotal);
        Assert.Equal(12.20m, order.GrossTotal);
        _fixture.UnitOfWork.Verify(work => work.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public void CartPreviewUsesNetPricesAndGroupsVatByRateAndNature()
    {
        var result = _fixture.OrderService.PreviewCassaCart(new(
        [
            new(_fixture.Variant.Id, 5, 18.84m, 22m, 10),
            new(_fixture.Variant.Id, 1, 30.00m, 10m, 10),
            new(_fixture.Variant.Id, 1, 50.00m, 0m, 10, "N3.2")
        ]));

        Assert.True(result.Success, result.Error);
        var preview = result.Data!;
        Assert.Equal([94.20m, 30.00m, 50.00m], preview.Lines.Select(line => line.LineNetTotal));
        Assert.Equal(3, preview.VatSummary.Count);
        Assert.Equal(174.20m, preview.TotalTaxable);
        Assert.Equal(23.72m, preview.TotalVat);
        Assert.Equal(197.92m, preview.TotalGross);
        _fixture.UnitOfWork.Verify(work => work.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public void CartPreviewComputesVatOncePerGroupNotPerLine()
    {
        var result = _fixture.OrderService.PreviewCassaCart(new(
            [new(_fixture.Variant.Id, 1, 0.03m, 22m, 10), new(_fixture.Variant.Id, 1, 0.03m, 22m, 10)]));

        Assert.True(result.Success, result.Error);
        Assert.Equal(0.06m, result.Data!.TotalTaxable);
        Assert.Equal(0.01m, result.Data.TotalVat);
        Assert.Equal(0.07m, result.Data.TotalGross);
    }

    [Fact]
    public void CartPreviewIncludesShippingAsAClassifiedLineInTotals()
    {
        var result = _fixture.OrderService.PreviewCassaCart(new(
            [new(_fixture.Variant.Id, 1, 10m, 22m, 10)],
            ShippingCost: 5.00m,
            ShippingVatRate: 10m));

        Assert.True(result.Success, result.Error);
        Assert.Equal([10.00m, 5.00m], result.Data!.Lines.Select(line => line.LineNetTotal));
        Assert.Equal(15.00m, result.Data.TotalTaxable);
        Assert.Equal(2.70m, result.Data.TotalVat);
        Assert.Equal(17.70m, result.Data.TotalGross);
    }

    [Fact]
    public void CartPreviewAppliesTheLineDiscountOnceBeforeVat()
    {
        var result = _fixture.OrderService.PreviewCassaCart(new(
            [new(_fixture.Variant.Id, 5, 10m, 22m, 10, DiscountPercent: 20m)]));

        Assert.True(result.Success, result.Error);
        Assert.Equal(20m, Assert.Single(result.Data!.Lines).DiscountPercent);
        Assert.Equal(40.00m, result.Data.TotalTaxable);
        Assert.Equal(8.80m, result.Data.TotalVat);
        Assert.Equal(48.80m, result.Data.TotalGross);
    }

    [Fact]
    public void CartPreviewShowsEachDiscountWithItsReasonAndTheDiscountedUnitPrice()
    {
        var result = _fixture.OrderService.PreviewCassaCart(new(
        [
            new(_fixture.Variant.Id, 5, 10m, 22m, 10, DiscountPercent: 10m, PriceListDiscountReason: "Sconto magliette",
                CashDiscount: new(5m, null, " Sconto alla cassa ")),
            new(_fixture.Variant.Id, 5, 10m, 22m, 10, CashDiscount: new(null, 5.00m, "Pezzo con difetto")),
            new(_fixture.Variant.Id, 5, 10m, 22m, 10, DiscountPercent: 10m),
            new(_fixture.Variant.Id, 5, 10m, 22m, 10)
        ]));

        Assert.True(result.Success, result.Error);
        var lines = result.Data!.Lines;
        Assert.Equal([42.75m, 45.00m, 45.00m, 50.00m], lines.Select(line => line.LineNetTotal));
        Assert.Equal([14.5m, 10m, 10m, 0m], lines.Select(line => line.DiscountPercent));
        Assert.Equal([8.55m, 9m, 9m, 10m], lines.Select(line => line.DiscountedUnitPrice));

        var composed = lines[0].Discounts!;
        Assert.Equal(["PriceList", "Cashier"], composed.Select(item => item.Source));
        Assert.Equal(["Sconto magliette", "Sconto alla cassa"], composed.Select(item => item.Reason));
        Assert.Equal([5.00m, 2.25m], composed.Select(item => item.SavedAmount));
        Assert.Equal(5.00m, Assert.Single(lines[1].Discounts!).Amount);
        Assert.Equal("Sconto listino", Assert.Single(lines[2].Discounts!).Reason);
        Assert.Empty(lines[3].Discounts!);
        Assert.Equal(182.75m, result.Data.TotalTaxable);
        Assert.Equal(40.21m, result.Data.TotalVat);
        Assert.Equal(222.96m, result.Data.TotalGross);
    }

    [Fact]
    public void CartPreviewRejectsCashDiscountWithoutReasonOnReturnsOrAboveTheLineTotal()
    {
        Assert.False(_fixture.OrderService.PreviewCassaCart(new(
            [new(_fixture.Variant.Id, 5, 10m, 22m, 10, CashDiscount: new(5m, null, " "))])).Success);
        Assert.False(_fixture.OrderService.PreviewCassaCart(new(
            [new(_fixture.Variant.Id, -1, 10m, 22m, 10, IsCustomerReturn: true, CashDiscount: new(5m, null, "Motivo"))])).Success);
        Assert.False(_fixture.OrderService.PreviewCassaCart(new(
            [new(_fixture.Variant.Id, 5, 10m, 22m, 10, CashDiscount: new(null, 50.01m, "Motivo"))])).Success);
        Assert.False(_fixture.OrderService.PreviewCassaCart(new(
            [new(_fixture.Variant.Id, 5, 10m, 22m, 10, CashDiscount: new(5m, 1m, "Motivo"))])).Success);
    }

    [Fact]
    public async Task CashDiscountOnAReturnIsRejectedBeforeSavingTheSession()
    {
        var result = await _fixture.OrderService.CreateCassaSessionAsync(new(_fixture.Contact.Id, null, null,
            [new(_fixture.Variant.Id, -1, 10m, 22m, 10, IsCustomerReturn: true, SourceLineId: Guid.NewGuid(),
                CashDiscount: new(5m, null, "Motivo"))]));

        Assert.False(result.Success);
        _fixture.UnitOfWork.Verify(work => work.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public void CartPreviewOfEmptyCartIsZero()
    {
        var result = _fixture.OrderService.PreviewCassaCart(new([]));

        Assert.True(result.Success, result.Error);
        Assert.Empty(result.Data!.Lines);
        Assert.Equal(0.00m, result.Data.TotalGross);
    }

    [Fact]
    public void CartPreviewRejectsZeroRateLineWithoutNature()
    {
        var result = _fixture.OrderService.PreviewCassaCart(new(
            [new(_fixture.Variant.Id, 1, 10m, 0m, 10)]));

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public async Task NullCartLineReturnsValidationErrorWithoutSaving()
    {
        var result = await _fixture.OrderService.CreateCassaSessionAsync(
            new(_fixture.Contact.Id, null, null, [null!]));

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        _fixture.UnitOfWork.Verify(work => work.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task CassaSessionCreationPersistsPreparedFiscalTotals()
    {
        Order? created = null;
        _fixture.UnitOfWork.Setup(work => work.Orders.AddWithNextNumberAsync(
                It.IsAny<Order>(), It.IsAny<string>()))
            .Callback<Order, string>((order, prefix) =>
            {
                order.OrderNumber = $"{prefix}001";
                created = order;
            })
            .Returns(Task.CompletedTask);
        _fixture.UnitOfWork.Setup(work => work.Orders.GetCassaSessionWithItemsAsync(It.IsAny<Guid>()))
            .ReturnsAsync(() => created);

        var result = await _fixture.OrderService.CreateCassaSessionAsync(new(
            _fixture.Contact.Id, "Rif-1", "terminale-1",
            [new(_fixture.Variant.Id, 2, 10m, 22m, 10)]));

        Assert.True(result.Success, result.Error);
        Assert.Equal("Cliente", result.Data!.ContactName);
        Assert.Equal("Rif-1", result.Data.DocumentReference);
        Assert.Single(result.Data.Items);
        Assert.Equal(OrderStatus.Suspended, created!.Status);
        Assert.Equal(OrderFulfillmentType.CashDesk, created.FulfillmentType);
        Assert.Equal("terminale-1", created.TerminalId);
        Assert.Equal(20m, created.TaxableTotal);
        Assert.Equal(4.40m, created.VatTotal);
        Assert.Equal(24.40m, created.GrossTotal);
        _fixture.UnitOfWork.Verify(work => work.Orders.AddWithNextNumberAsync(
            It.IsAny<Order>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CassaSessionCreationRejectsUnknownCustomer()
    {
        var result = await _fixture.OrderService.CreateCassaSessionAsync(new(
            Guid.NewGuid(), null, null, []));

        Assert.False(result.Success);
        Assert.Equal("Cliente non trovato", result.Error);
        _fixture.UnitOfWork.Verify(work => work.Orders.AddWithNextNumberAsync(
            It.IsAny<Order>(), It.IsAny<string>()), Times.Never);
        _fixture.UnitOfWork.Verify(work => work.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task CassaSessionUpdateReplacesItemsAndPersistsTotals()
    {
        var order = new Order(_fixture.Contact.Id, "ORD-TEST");
        _fixture.UnitOfWork.Setup(work => work.Orders.GetCassaSessionWithItemsAsync(order.Id))
            .ReturnsAsync(order);
        _fixture.UnitOfWork.Setup(work => work.Orders.UpdateAsync(order)).Returns(Task.CompletedTask);

        var result = await _fixture.OrderService.UpdateCassaSessionAsync(order.Id, new(
            _fixture.Contact.Id, "Rif-2", "terminale-2",
            [new(_fixture.Variant.Id, 2, 10m, 22m, 10)]));

        Assert.True(result.Success, result.Error);
        Assert.Equal("Rif-2", order.DocumentReference);
        Assert.Equal(OrderStatus.Suspended, order.Status);
        Assert.Equal("terminale-2", order.TerminalId);
        Assert.Equal(20m, order.TaxableTotal);
        Assert.Equal(24.40m, order.GrossTotal);
        Assert.Single(order.Items);
        _fixture.UnitOfWork.Verify(work => work.Orders.UpdateAsync(order), Times.Once);
        _fixture.UnitOfWork.Verify(work => work.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CassaSessionUpdateRejectsMissingOrClosedSessions()
    {
        var orderId = Guid.NewGuid();
        _fixture.UnitOfWork.Setup(work => work.Orders.GetCassaSessionWithItemsAsync(orderId))
            .ReturnsAsync((Order?)null);
        var missing = await _fixture.OrderService.UpdateCassaSessionAsync(orderId,
            new(_fixture.Contact.Id, null, null, []));
        Assert.Equal("Sessione non trovata", missing.Error);

        var order = new Order(_fixture.Contact.Id, "ORD-CLOSED");
        order.Items.Add(new OrderItem(order.Id, _fixture.Variant.Id, 1, 10m, 22m));
        order.Confirm();
        _fixture.UnitOfWork.Setup(work => work.Orders.GetCassaSessionWithItemsAsync(order.Id))
            .ReturnsAsync(order);
        var closed = await _fixture.OrderService.UpdateCassaSessionAsync(order.Id,
            new(_fixture.Contact.Id, null, null, []));
        Assert.Equal("Non e possibile modificare una sessione confermata o terminata.", closed.Error);
    }

    [Fact]
    public async Task CassaSessionCanResumeTerminateAndBeRead()
    {
        var order = new Order(_fixture.Contact.Id, "ORD-SESSION") { Contact = _fixture.Contact };
        order.Suspend("terminal-2");
        _fixture.UnitOfWork.Setup(work => work.Orders.GetCassaSessionWithItemsAsync(order.Id))
            .ReturnsAsync(order);
        _fixture.UnitOfWork.Setup(work => work.Orders.GetByIdAsync(order.Id)).ReturnsAsync(order);
        _fixture.UnitOfWork.Setup(work => work.Orders.UpdateAsync(order)).Returns(Task.CompletedTask);

        var resumed = await _fixture.OrderService.ResumeCassaSessionAsync(order.Id, "terminal-3");
        Assert.True(resumed.Success, resumed.Error);
        Assert.Equal(OrderStatus.Draft, order.Status);
        Assert.Equal("terminal-3", order.TerminalId);
        var details = await _fixture.OrderService.GetCassaSessionAsync(order.Id);
        var terminated = await _fixture.OrderService.TerminateCassaSessionAsync(order.Id);

        Assert.Equal("Cliente", details.Data!.ContactName);
        Assert.True(terminated.Success, terminated.Error);
        Assert.Equal(OrderStatus.Terminated, order.Status);
        _fixture.UnitOfWork.Verify(work => work.SaveChangesAsync(), Times.Exactly(2));
    }

    [Fact]
    public async Task CassaSessionReadersHandleMissingOrdersAndListSuspendedSessions()
    {
        var id = Guid.NewGuid();
        _fixture.UnitOfWork.Setup(work => work.Orders.GetCassaSessionWithItemsAsync(id))
            .ReturnsAsync((Order?)null);
        _fixture.UnitOfWork.Setup(work => work.Orders.GetByIdAsync(id)).ReturnsAsync((Order?)null);
        Assert.Equal("Sessione non trovata",
            (await _fixture.OrderService.GetCassaSessionAsync(id)).Error);
        Assert.Equal("Sessione non trovata",
            (await _fixture.OrderService.ResumeCassaSessionAsync(id, null)).Error);
        Assert.Equal("Sessione non trovata",
            (await _fixture.OrderService.TerminateCassaSessionAsync(id)).Error);

        var suspended = new Order(_fixture.Contact.Id, "ORD-SUSPENDED") { Contact = _fixture.Contact };
        suspended.Items.Add(new OrderItem(suspended.Id, _fixture.Variant.Id, 1, 10m, 22m));
        _fixture.UnitOfWork.Setup(work => work.Orders.GetSuspendedCassaSessionsAsync())
            .ReturnsAsync([suspended]);

        var result = await _fixture.OrderService.GetSuspendedSessionsAsync();

        var summary = Assert.Single(result.Data!);
        Assert.Equal("ORD-SUSPENDED", summary.SessionCode);
        Assert.Equal("Cliente", summary.ContactName);
        Assert.Equal(1, summary.CartItemCount);
    }

    private sealed class Fixture
    {
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public ItalianAccountingCalculator Calculator { get; } = new();
        public Contact Contact { get; } = new("Cliente");
        public ProductVariant Variant { get; }
        public OrderService OrderService { get; }

        public Fixture()
        {
            var product = new Product(Guid.NewGuid(), "SUP", "SKU", "Articolo", 22m, 10m);
            Variant = new(product.Id, "123", 10m, product.SupplierId);
            UnitOfWork.Setup(work => work.ProductVariants.GetByIdAsync(Variant.Id)).ReturnsAsync(Variant);
            UnitOfWork.Setup(work => work.Products.GetByIdAsync(product.Id)).ReturnsAsync(product);
            UnitOfWork.Setup(work => work.Contacts.GetByIdAsync(Contact.Id)).ReturnsAsync(Contact);
            UnitOfWork.Setup(work => work.SaveChangesAsync()).Returns(Task.CompletedTask);
            var settings = Mock.Of<ICompanySettingsService>();
            var pdf = Mock.Of<IInvoicePdfGenerator>();
            var pricingRepository = new Mock<IPriceListRepository>();
            pricingRepository.Setup(repository => repository.GetVariantsAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { Variant });
            var pricing = new DocumentPricingService(pricingRepository.Object, Mock.Of<IPriceListService>(), Calculator);
            OrderService = new(UnitOfWork.Object, Mock.Of<IValidator<CreateOrderRequest>>(),
                new CreateOrderItemRequestValidator(), Calculator, settings, pdf,
                NullLogger<OrderService>.Instance, pricing);
        }
    }
}
