using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Interfaces;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Domain.Services;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

public class InvoiceManagementServiceTests
{
    [Fact]
    public void ConfirmedInvoiceCanBeEditedUntilSdiSubmissionAndCannotBeConfirmedAgain()
    {
        var invoice = new Invoice(Guid.NewGuid(), "INV-TEST");
        var calculator = new ItalianAccountingCalculator();
        Assert.True(invoice.AddItem(
            new(invoice.Id, Guid.NewGuid(), 1, 100m, 22m), calculator).Success);

        var result = invoice.Confirm(calculator);

        Assert.True(result.Success, result.Error);
        Assert.Equal("Confirmed", invoice.Status);
        Assert.NotNull(invoice.ConfirmedAt);
        var preSdiAddition = invoice.AddItem(
            new(invoice.Id, Guid.NewGuid(), 1, 1m, 22m), calculator);
        Assert.True(preSdiAddition.Success, preSdiAddition.Error);
        Assert.Equal("Confirmed", invoice.Status);
        Assert.False(invoice.Confirm(calculator).Success);
    }

    [Fact]
    public void CannotConfirmAnEmptyInvoice()
    {
        var invoice = new Invoice(Guid.NewGuid(), "INV-EMPTY");

        var result = invoice.Confirm(new ItalianAccountingCalculator());

        Assert.False(result.Success);
        Assert.Equal("Draft", invoice.Status);
        Assert.Null(invoice.ConfirmedAt);
    }

    [Fact]
    public async Task RecordsPaymentInInvoiceAndReturnsUpdatedBalance()
    {
        var (service, unitOfWork, repository, invoice) = CreateService(grossTotal: 125m);
        repository.Setup(value => value.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);

        var result = await service.RecordPaymentAsync(invoice.Id, new()
        {
            Amount = 25.50m,
            PaymentDate = new DateTime(2026, 10, 29),
            PaymentMethod = "Bonifico bancario",
            TransactionDetails = "CRO 123456"
        });

        Assert.True(result.Success, result.Error);
        Assert.Equal(25.50m, result.Data!.PaidAmount);
        Assert.Equal(99.50m, result.Data.BalanceDue);
        Assert.Equal("PartiallyPaid", result.Data.Status);
        var paymentDto = Assert.Single(result.Data.Payments);
        Assert.Equal("Bonifico bancario", paymentDto.PaymentMethod);
        Assert.Equal("CRO 123456", paymentDto.TransactionDetails);
        var payment = Assert.Single(invoice.Payments);
        Assert.Equal(25.50m, payment.Amount);
        Assert.Equal("Bonifico bancario", payment.PaymentMethod);
        Assert.Equal("CRO 123456", payment.TransactionDetails);
        repository.Verify(value => value.UpdateAsync(invoice), Times.Once);
        unitOfWork.Verify(value => value.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task RecordsFinalPaymentAndMovesInvoiceToPaid()
    {
        var (service, _, repository, invoice) = CreateService(grossTotal: 125m);
        repository.Setup(value => value.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);

        var result = await service.RecordPaymentAsync(invoice.Id, new()
        {
            Amount = 125m,
            PaymentMethod = "Carta"
        });

        Assert.True(result.Success, result.Error);
        Assert.Equal("Paid", result.Data!.Status);
        Assert.Equal(0m, result.Data.BalanceDue);
    }

    public static TheoryData<decimal> InvalidPayments => new()
    {
        0m, -1m, 1.001m, 100.01m
    };

    [Theory]
    [MemberData(nameof(InvalidPayments))]
    public async Task RejectsInvalidOrExcessPaymentWithoutMutation(decimal amount)
    {
        var (service, unitOfWork, repository, invoice) = CreateService(grossTotal: 100m);
        repository.Setup(value => value.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);

        var result = await service.RecordPaymentAsync(invoice.Id, new()
        {
            Amount = amount,
            PaymentMethod = "Carta"
        });

        Assert.False(result.Success);
        Assert.Empty(invoice.Payments);
        Assert.Equal(0.00m, invoice.PaidAmount);
        repository.Verify(value => value.UpdateAsync(It.IsAny<Invoice>()), Times.Never);
        unitOfWork.Verify(value => value.SaveChangesAsync(), Times.Never);
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("Bonifico bancario", null)]
    [InlineData("Bonifico bancario", "  ")]
    public async Task RejectsMissingPaymentMethodOrBankTransferReference(string method, string? details)
    {
        var (service, unitOfWork, repository, invoice) = CreateService(grossTotal: 100m);
        repository.Setup(value => value.GetByIdAsync(invoice.Id)).ReturnsAsync(invoice);

        var result = await service.RecordPaymentAsync(invoice.Id, new()
        {
            Amount = 10m,
            PaymentMethod = method,
            TransactionDetails = details
        });

        Assert.False(result.Success);
        Assert.Empty(invoice.Payments);
        Assert.Equal(0.00m, invoice.PaidAmount);
        repository.Verify(value => value.UpdateAsync(It.IsAny<Invoice>()), Times.Never);
        unitOfWork.Verify(value => value.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task ValidatesPagingBeforeQueryingInvoices()
    {
        var (service, _, repository, _) = CreateService();

        var result = await service.GetInvoicesAsync(null, null, 0, 20);

        Assert.False(result.Success);
        repository.Verify(value => value.GetPagedAsync(
            It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<string?>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<bool>(),
            It.IsAny<string?>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task RejectsInvalidDateRangeBeforeQueryingInvoices()
    {
        var (service, _, repository, _) = CreateService();

        var result = await service.GetInvoicesAsync(
            null, null, 1, 20, dateFrom: new DateOnly(2026, 10, 31), dateTo: new DateOnly(2026, 10, 1));

        Assert.False(result.Success);
        Assert.Contains("data iniziale", result.Error, StringComparison.OrdinalIgnoreCase);
        repository.Verify(value => value.GetPagedAsync(
            It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<string?>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<bool>(),
            It.IsAny<string?>(), It.IsAny<bool>()), Times.Never);
    }

    private static (InvoiceService Service, Mock<IUnitOfWork> UnitOfWork,
        Mock<IInvoiceRepository> Repository, Invoice Invoice) CreateService(decimal grossTotal = 0m)
    {
        var contact = new Contact("Cliente");
        var invoice = new Invoice(contact.Id, "INV-TEST") { Contact = contact };
        var calculator = new ItalianAccountingCalculator();
        if (grossTotal > 0m)
        {
            Assert.True(invoice.AddItem(
                new(invoice.Id, Guid.NewGuid(), 1, grossTotal, 0m, "N3.2"), calculator).Success);
            Assert.True(invoice.Confirm(calculator).Success);
        }
        var repository = new Mock<IInvoiceRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(value => value.Invoices).Returns(repository.Object);
        unitOfWork.Setup(value => value.SaveChangesAsync()).Returns(Task.CompletedTask);
        repository.Setup(value => value.UpdateAsync(It.IsAny<Invoice>())).Returns(Task.CompletedTask);

        var service = new InvoiceService(
            unitOfWork.Object,
            calculator,
            Mock.Of<ICompanySettingsService>(),
            Mock.Of<IInvoicePdfGenerator>());
        return (service, unitOfWork, repository, invoice);
    }
}
