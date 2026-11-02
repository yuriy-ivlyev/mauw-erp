using Mauw.Erp.Application.Commands.CreditNotes;
using Mauw.Erp.Application.Commands.Invoices;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Mauw.Erp.Application.Commands.FiscalDocuments;

public sealed record BulkTransmitFiscalDocumentsCommand(
    IReadOnlyList<FiscalDocumentTransmissionRequest> Documents)
    : IRequest<OperationResult<BulkFiscalTransmissionResultDto>>;

public sealed class BulkTransmitFiscalDocumentsCommandHandler(
    ISender sender,
    ILogger<BulkTransmitFiscalDocumentsCommandHandler> logger)
    : IRequestHandler<BulkTransmitFiscalDocumentsCommand, OperationResult<BulkFiscalTransmissionResultDto>>
{
    public async Task<OperationResult<BulkFiscalTransmissionResultDto>> Handle(
        BulkTransmitFiscalDocumentsCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Documents is null || command.Documents.Count == 0)
            return OperationResult<BulkFiscalTransmissionResultDto>.Fail(
                "Selezionare almeno un documento fiscale da trasmettere.");

        var results = new List<FiscalDocumentTransmissionItemResultDto>(command.Documents.Count);
        foreach (var document in command.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (document is null || document.Id == Guid.Empty)
            {
                results.Add(new(
                    document?.Id ?? Guid.Empty,
                    document?.DocumentType ?? string.Empty,
                    false,
                    null,
                    "Identificativo documento non valido."));
                continue;
            }

            var documentType = document.DocumentType?.Trim();
            if (string.Equals(documentType, "Invoice", StringComparison.OrdinalIgnoreCase))
            {
                await ProcessInvoiceAsync(document.Id, results, cancellationToken);
                continue;
            }
            if (string.Equals(documentType, "CreditNote", StringComparison.OrdinalIgnoreCase))
            {
                await ProcessCreditNoteAsync(document.Id, results, cancellationToken);
                continue;
            }

            results.Add(new(
                document.Id,
                documentType ?? string.Empty,
                false,
                null,
                "Tipologia documento non supportata."));
        }

        var successCount = results.Count(result => result.Success);
        return OperationResult<BulkFiscalTransmissionResultDto>.Ok(new(
            results.Count,
            successCount,
            results.Count - successCount,
            results));
    }

    private async Task ProcessInvoiceAsync(
        Guid invoiceId,
        List<FiscalDocumentTransmissionItemResultDto> results,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(
                new TransmitInvoiceToSdiCommand(invoiceId), cancellationToken);
            results.Add(new(
                invoiceId,
                "Invoice",
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
                "Invoice",
                false,
                null,
                "Errore tecnico durante la trasmissione. Contattare l'amministratore."));
        }
    }

    private async Task ProcessCreditNoteAsync(
        Guid creditNoteId,
        List<FiscalDocumentTransmissionItemResultDto> results,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(
                new TransmitCreditNoteToSdiCommand(creditNoteId), cancellationToken);
            results.Add(new(
                creditNoteId,
                "CreditNote",
                result.Success,
                result.Data?.CreditNoteNumber,
                result.Success ? null : result.Error ?? "Trasmissione SDI non riuscita."));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Errore durante l'invio batch allo SDI della nota di credito {CreditNoteId}.",
                creditNoteId);
            results.Add(new(
                creditNoteId,
                "CreditNote",
                false,
                null,
                "Errore tecnico durante la trasmissione. Contattare l'amministratore."));
        }
    }
}
