using System.Globalization;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Contracts.Interfaces;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Domain.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

public class DocumentAccountingServiceTests
{
    private readonly Fixture _fixture = new();

    [Fact]
    public async Task FreeInvoiceLineUsesAuthoritativeVatRateWithoutDivisionByZero()
    {
        var result = await _fixture.InvoiceService.AddItemAsync(_fixture.Invoice.Id,
            new() { VariantId = _fixture.Variant.Id, Quantity = 1, UnitNetPrice = 0m, VatRate = 22m });

        Assert.True(result.Success, result.Error);
        Assert.Equal(0m, result.Data!.GrossTotal);
        Assert.Equal(22m, Assert.Single(result.Data.Items).VatRate);
        _fixture.UnitOfWork.Verify(work => work.SaveChangesAsync(), Times.Once);
    }

    [Theory]
    [InlineData("23", "94.26", "20.74", "115.00")]
    [InlineData("94.18", "385.98", "84.92", "470.90")]
    public async Task InvoiceScorporoPersistsEightPlacePriceAndFiscalTotals(
        string gross, string taxable, string vat, string total)
    {
        var result = await _fixture.InvoiceService.AddItemAsync(_fixture.Invoice.Id,
            new()
            {
                VariantId = _fixture.Variant.Id, Quantity = 5,
                UnitGrossPrice = Parse(gross), VatRate = 22m
            });

        Assert.True(result.Success, result.Error);
        Assert.Equal(Parse(taxable), result.Data!.TaxableTotal);
        Assert.Equal(Parse(vat), result.Data.VatTotal);
        Assert.Equal(Parse(total), result.Data.GrossTotal);
        var line = Assert.Single(result.Data.Items);
        Assert.Equal(Math.Round(line.UnitNetPrice * 5m, 2, MidpointRounding.AwayFromZero), line.LineNetTotal);
        Assert.Null(line.LineVatTotal);
        Assert.Null(line.LineGrossTotal);
    }

    [Fact]
    public async Task InvoiceAdditionRecalculatesGroupRatherThanSummingLineTaxes()
    {
        var request = new CreateInvoiceItemRequest
        {
            VariantId = _fixture.Variant.Id, Quantity = 1, UnitNetPrice = 0.03m, VatRate = 22m
        };
        Assert.True((await _fixture.InvoiceService.AddItemAsync(_fixture.Invoice.Id, request)).Success);

        var result = await _fixture.InvoiceService.AddItemAsync(_fixture.Invoice.Id, request);

        Assert.True(result.Success, result.Error);
        Assert.Equal(0.06m, result.Data!.TaxableTotal);
        Assert.Equal(0.01m, result.Data.VatTotal);
        Assert.Equal(0.07m, result.Data.GrossTotal);
    }

    [Fact]
    public async Task MissingNatureFailsWithoutSavingInvoice()
    {
        var result = await _fixture.InvoiceService.AddItemAsync(_fixture.Invoice.Id,
            new() { VariantId = _fixture.Variant.Id, Quantity = 1, UnitNetPrice = 10m, VatRate = 0m });

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        Assert.Empty(_fixture.Invoice.Items);
        _fixture.UnitOfWork.Verify(work => work.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task InvalidCartIsRejectedBeforeCreatingOrSavingAnOrder()
    {
        var result = await _fixture.OrderService.CreateCassaSessionAsync(new(_fixture.Contact.Id, null, null,
            [new(_fixture.Variant.Id, 1, 10m, 22m, 10), new(_fixture.Variant.Id, 1, 10m, 0m, 10)]));

        Assert.False(result.Success);
        _fixture.UnitOfWork.Verify(work => work.Orders.AddAsync(It.IsAny<Order>()), Times.Never);
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

    private static decimal Parse(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

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

    private sealed class Fixture
    {
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public ItalianAccountingCalculator Calculator { get; } = new();
        public Contact Contact { get; } = new("Cliente");
        public Invoice Invoice { get; }
        public ProductVariant Variant { get; }
        public InvoiceService InvoiceService { get; }
        public OrderService OrderService { get; }

        public Fixture()
        {
            var product = new Product(Guid.NewGuid(), "SUP", "SKU", "Articolo", 0m, 22m, 10m);
            Variant = new(product.Id, "123", 10m);
            Invoice = new(Contact.Id, "INV-TEST");
            UnitOfWork.Setup(work => work.Invoices.GetByIdAsync(Invoice.Id)).ReturnsAsync(Invoice);
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
            InvoiceService = new(UnitOfWork.Object, Mock.Of<IValidator<CreateInvoiceRequest>>(),
                Calculator, settings, pdf, pricing);
            OrderService = new(UnitOfWork.Object, Mock.Of<IValidator<CreateOrderRequest>>(),
                new CreateOrderItemRequestValidator(), Calculator, settings, pdf,
                NullLogger<OrderService>.Instance, pricing);
        }
    }
}
