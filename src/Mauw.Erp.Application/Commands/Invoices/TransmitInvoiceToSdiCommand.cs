using System.Data;
using System.Xml;
using System.Xml.Linq;
using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Exceptions;
using Mauw.Erp.Domain.Repositories;
using MediatR;

namespace Mauw.Erp.Application.Commands.Invoices;

public sealed record TransmitInvoiceToSdiCommand(Guid InvoiceId) : IRequest<OperationResult<InvoiceDto>>;

public sealed class TransmitInvoiceToSdiCommandHandler(
    IUnitOfWork unitOfWork,
    IInvoiceService invoiceService,
    ISdiDocumentStorage documentStorage,
    ISdiTransmissionProvider transmissionProvider,
    ICurrentUserService currentUser)
    : IRequestHandler<TransmitInvoiceToSdiCommand, OperationResult<InvoiceDto>>
{
    public async Task<OperationResult<InvoiceDto>> Handle(
        TransmitInvoiceToSdiCommand command,
        CancellationToken cancellationToken)
    {
        if (command.InvoiceId == Guid.Empty)
            return OperationResult<InvoiceDto>.Fail("Fattura non valida.");

        var transactionStarted = false;
        string? storageKey = null;
        try
        {
            await unitOfWork.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            transactionStarted = true;

            var orderId = await unitOfWork.Invoices.GetOrderIdAsync(command.InvoiceId);
            if (orderId.HasValue)
                await unitOfWork.Invoices.AcquireOrderInvoiceLockAsync(orderId.Value);
            else
                await unitOfWork.Invoices.AcquireSdiTransmissionLockAsync(command.InvoiceId);

            var invoice = await unitOfWork.Invoices.GetByIdAsync(command.InvoiceId);
            if (invoice is null)
                return await FailAsync("Fattura non trovata.");
            if (invoice.SdiStatus != SdiStatus.NotTransmitted)
                return await FailAsync(
                    $"La fattura non è in stato NotTransmitted (stato corrente: {invoice.SdiStatus}).");
            if (invoice.OrderId.HasValue &&
                await unitOfWork.Orders.IsUnderAdjustmentAsync(invoice.OrderId.Value))
                return await FailAsync("La fattura non può essere trasmessa mentre l'ordine è in rettifica pre-SDI.");

            cancellationToken.ThrowIfCancellationRequested();
            var xmlResult = await invoiceService.GetInvoiceXmlAsync(invoice.Id);
            if (!xmlResult.Success || string.IsNullOrWhiteSpace(xmlResult.Data))
                return await FailAsync(xmlResult.Error ?? "Impossibile generare l'XML FatturaPA.");

            var xml = xmlResult.Data;
            var validationErrors = FatturaPaSchemaValidator.Validate(xml);
            if (validationErrors.Count > 0)
                return await FailAsync(
                    "L'XML FatturaPA non supera la validazione XSD: " + string.Join(" ", validationErrors));

            var fileNameResult = BuildSdiFileName(xml);
            if (!fileNameResult.Success)
                return await FailAsync(fileNameResult.Error!);

            var fileName = fileNameResult.Data!;
            var xmlContent = System.Text.Encoding.UTF8.GetBytes(xml);
            storageKey = await documentStorage.UploadInvoiceXmlAsync(fileName, xmlContent, cancellationToken);

            var transmission = await transmissionProvider.TransmitAsync(fileName, xmlContent, cancellationToken);
            if (!transmission.Success)
                return await FailAsync(
                    transmission.ErrorMessage ?? "La trasmissione della fattura allo SDI non è riuscita.");

            var transmittedAtUtc = DateTime.UtcNow;
            var marked = invoice.MarkAsTransmitted(
                fileName, storageKey, transmission.TransmissionId, transmittedAtUtc);
            if (!marked.Success)
                return await FailAsync(marked.Error!);

            var transmissionId = transmission.TransmissionId ?? "non disponibile";
            await unitOfWork.AuditTrail.AddAsync(new DocumentAuditEntry(
                AuditDocumentTypes.Invoice,
                invoice.Id,
                AuditActions.InvoiceTransmittedToSdi,
                $"Fattura trasmessa a SDI con file {fileName} e ID {transmissionId}",
                currentUser.UserId,
                currentUser.FullName ?? currentUser.Email,
                occurredAtUtc: transmittedAtUtc));

            await unitOfWork.SaveChangesAsync();
            await unitOfWork.CommitAsync();
            transactionStarted = false;
            storageKey = null;
            return OperationResult<InvoiceDto>.Ok(InvoiceMapper.MapToDto(invoice));
        }
        catch (Exception exception)
        {
            var cleanupErrors = new List<Exception>();
            if (transactionStarted)
            {
                try
                {
                    await unitOfWork.RollbackAsync();
                    transactionStarted = false;
                }
                catch (Exception rollbackException)
                {
                    cleanupErrors.Add(rollbackException);
                }
            }

            if (storageKey is not null)
            {
                try
                {
                    await documentStorage.DeleteInvoiceXmlAsync(storageKey, CancellationToken.None);
                    storageKey = null;
                }
                catch (Exception deleteException)
                {
                    cleanupErrors.Add(deleteException);
                }
            }

            if (cleanupErrors.Count > 0)
                throw new AggregateException(
                    "La trasmissione SDI è fallita e non è stato possibile completare il rollback o la pulizia dello storage.",
                    [exception, .. cleanupErrors]);

            if (exception is OperationCanceledException)
                throw;
            if (exception is InvalidOperationException or ArgumentException or DomainException or ConcurrencyException)
                return OperationResult<InvoiceDto>.Fail(exception.Message);

            throw;
        }

        async Task<OperationResult<InvoiceDto>> FailAsync(string error)
        {
            if (transactionStarted)
            {
                await unitOfWork.RollbackAsync();
                transactionStarted = false;
            }

            if (storageKey is not null)
            {
                await documentStorage.DeleteInvoiceXmlAsync(storageKey, CancellationToken.None);
                storageKey = null;
            }

            return OperationResult<InvoiceDto>.Fail(error);
        }
    }

    private static OperationResult<string> BuildSdiFileName(string xml)
    {
        try
        {
            var document = XDocument.Parse(xml);
            var transmissionData = document.Root?
                .Element("FatturaElettronicaHeader")?
                .Element("DatiTrasmissione");
            var country = transmissionData?
                .Element("IdTrasmittente")?
                .Element("IdPaese")?.Value;
            var vatNumber = transmissionData?
                .Element("IdTrasmittente")?
                .Element("IdCodice")?.Value;
            var progressive = transmissionData?.Element("ProgressivoInvio")?.Value;

            if (!string.Equals(country, "IT", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(vatNumber) ||
                string.IsNullOrWhiteSpace(progressive))
                return OperationResult<string>.Fail(
                    "L'XML FatturaPA non contiene una partita IVA italiana e un ProgressivoInvio validi.");

            var fileName = $"IT{vatNumber}_{progressive}.xml";
            return fileName.Length <= 50
                ? OperationResult<string>.Ok(fileName)
                : OperationResult<string>.Fail("Il nome file SDI supera il limite di 50 caratteri.");
        }
        catch (XmlException exception)
        {
            return OperationResult<string>.Fail($"L'XML FatturaPA non è valido: {exception.Message}");
        }
    }
}
