using Mauw.Erp.Application.Commands.Invoices;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

public sealed class BulkTransmitInvoicesToSdiCommandTests
{
    [Fact]
    public async Task ProcessesEveryInvoiceAndReturnsIndividualSuccessAndFailureCounts()
    {
        var successId = Guid.NewGuid();
        var failureId = Guid.NewGuid();
        var exceptionId = Guid.NewGuid();
        var sender = new Mock<ISender>(MockBehavior.Strict);
        sender.Setup(value => value.Send(
                It.IsAny<IRequest<OperationResult<InvoiceDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((IRequest<OperationResult<InvoiceDto>> request, CancellationToken _) =>
            {
                var invoiceId = ((TransmitInvoiceToSdiCommand)request).InvoiceId;
                if (invoiceId == exceptionId)
                    throw new InvalidOperationException("Provider unavailable.");
                return Task.FromResult(invoiceId == successId
                    ? OperationResult<InvoiceDto>.Ok(CreateInvoice(successId))
                    : OperationResult<InvoiceDto>.Fail("Invoice is already transmitted."));
            });
        var handler = new BulkTransmitInvoicesToSdiCommandHandler(
            sender.Object, NullLogger<BulkTransmitInvoicesToSdiCommandHandler>.Instance);

        var result = await handler.Handle(
            new BulkTransmitInvoicesToSdiCommand([successId, Guid.Empty, failureId, exceptionId]),
            CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.Equal(4, result.Data!.RequestedCount);
        Assert.Equal(1, result.Data.SuccessCount);
        Assert.Equal(3, result.Data.FailedCount);
        Assert.Collection(result.Data.Results,
            item =>
            {
                Assert.Equal(successId, item.InvoiceId);
                Assert.True(item.Success);
                Assert.Equal("FAT-1", item.InvoiceNumber);
            },
            item =>
            {
                Assert.Equal(Guid.Empty, item.InvoiceId);
                Assert.False(item.Success);
                Assert.Equal("Identificativo fattura non valido.", item.ErrorMessage);
            },
            item =>
            {
                Assert.Equal(failureId, item.InvoiceId);
                Assert.False(item.Success);
                Assert.Equal("Invoice is already transmitted.", item.ErrorMessage);
            },
            item =>
            {
                Assert.Equal(exceptionId, item.InvoiceId);
                Assert.False(item.Success);
                Assert.Contains("Errore tecnico", item.ErrorMessage);
            });
        sender.Verify(value => value.Send(
            It.IsAny<IRequest<OperationResult<InvoiceDto>>>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task RejectsAnEmptySelectionWithoutCallingTheSender()
    {
        var sender = new Mock<ISender>(MockBehavior.Strict);
        var handler = new BulkTransmitInvoicesToSdiCommandHandler(
            sender.Object, NullLogger<BulkTransmitInvoicesToSdiCommandHandler>.Instance);

        var result = await handler.Handle(
            new BulkTransmitInvoicesToSdiCommand([]), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("almeno una", result.Error);
        sender.VerifyNoOtherCalls();
    }

    private static InvoiceDto CreateInvoice(Guid id) => new(
        id,
        "FAT-1",
        Guid.NewGuid(),
        null,
        "Cliente",
        DateTime.UtcNow,
        0m,
        0m,
        0m,
        0m,
        null,
        null,
        null,
        0m,
        0m,
        "Confirmed",
        [],
        []);
}
