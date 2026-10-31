using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Mauw.Erp.Application.Commands.Invoices;

public sealed record BulkTransmitInvoicesToSdiCommand(IReadOnlyList<Guid> InvoiceIds)
    : IRequest<OperationResult<BulkTransmissionResultDto>>;

public sealed class BulkTransmitInvoicesToSdiCommandHandler(
    ISender sender,
    ILogger<BulkTransmitInvoicesToSdiCommandHandler> logger)
    : IRequestHandler<BulkTransmitInvoicesToSdiCommand, OperationResult<BulkTransmissionResultDto>>
{
    public async Task<OperationResult<BulkTransmissionResultDto>> Handle(
        BulkTransmitInvoicesToSdiCommand command,
        CancellationToken cancellationToken)
    {
        if (command.InvoiceIds is null || command.InvoiceIds.Count == 0)
            return OperationResult<BulkTransmissionResultDto>.Fail(
                "Selezionare almeno una fattura da trasmettere.");

        var results = new List<BulkTransmissionItemResultDto>(command.InvoiceIds.Count);
        foreach (var invoiceId in command.InvoiceIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (invoiceId == Guid.Empty)
            {
                results.Add(new(invoiceId, false, null, "Identificativo fattura non valido."));
                continue;
            }

            try
            {
                var result = await sender.Send(new TransmitInvoiceToSdiCommand(invoiceId), cancellationToken);
                results.Add(new(
                    invoiceId,
                    result.Success,
                    result.Data?.InvoiceNumber,
                    result.Success ? null : result.Error ?? "Trasmissione SDI non riuscita."));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception,
                    "Errore durante l'invio batch allo SDI della fattura {InvoiceId}.", invoiceId);
                results.Add(new(
                    invoiceId,
                    false,
                    null,
                    "Errore tecnico durante la trasmissione. Contattare l'amministratore."));
            }
        }

        var successCount = results.Count(result => result.Success);
        return OperationResult<BulkTransmissionResultDto>.Ok(new(
            RequestedCount: results.Count,
            SuccessCount: successCount,
            FailedCount: results.Count - successCount,
            Results: results));
    }
}
