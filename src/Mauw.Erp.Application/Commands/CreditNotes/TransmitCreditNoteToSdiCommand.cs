using System.Data;
using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Exceptions;
using Mauw.Erp.Domain.Repositories;
using MediatR;

namespace Mauw.Erp.Application.Commands.CreditNotes;

public sealed record TransmitCreditNoteToSdiCommand(Guid CreditNoteId)
    : IRequest<OperationResult<CreditNoteSummaryDto>>;

public sealed class TransmitCreditNoteToSdiCommandHandler(
    IUnitOfWork unitOfWork,
    IInvoiceService invoiceService,
    ISdiDocumentStorage documentStorage,
    ISdiTransmissionProvider transmissionProvider,
    ICurrentUserService currentUser)
    : IRequestHandler<TransmitCreditNoteToSdiCommand, OperationResult<CreditNoteSummaryDto>>
{
    public async Task<OperationResult<CreditNoteSummaryDto>> Handle(
        TransmitCreditNoteToSdiCommand command,
        CancellationToken cancellationToken)
    {
        if (command.CreditNoteId == Guid.Empty)
            return OperationResult<CreditNoteSummaryDto>.Fail("Nota di credito non valida.");

        var transactionStarted = false;
        string? storageKey = null;
        try
        {
            await unitOfWork.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            transactionStarted = true;
            await unitOfWork.CreditNotes.AcquireSdiTransmissionLockAsync(command.CreditNoteId);

            var creditNote = await unitOfWork.CreditNotes.GetByIdAsync(command.CreditNoteId);
            if (creditNote is null)
                return await FailAsync("Nota di credito non trovata.");
            if (creditNote.SdiStatus != SdiStatus.NotTransmitted)
                return await FailAsync(
                    $"La nota di credito non è in stato NotTransmitted (stato corrente: {creditNote.SdiStatus}).");

            cancellationToken.ThrowIfCancellationRequested();
            var xmlResult = await invoiceService.GetCreditNoteXmlAsync(creditNote.Id);
            if (!xmlResult.Success || string.IsNullOrWhiteSpace(xmlResult.Data))
                return await FailAsync(xmlResult.Error ?? "Impossibile generare l'XML TD04.");

            var xml = xmlResult.Data;
            var validationErrors = FatturaPaSchemaValidator.Validate(xml);
            if (validationErrors.Count > 0)
                return await FailAsync(
                    "L'XML TD04 non supera la validazione XSD: " + string.Join(" ", validationErrors));

            var fileNameResult = SdiFileNameBuilder.Build(xml);
            if (!fileNameResult.Success)
                return await FailAsync(fileNameResult.Error!);

            var fileName = fileNameResult.Data!;
            var xmlContent = System.Text.Encoding.UTF8.GetBytes(xml);
            storageKey = await documentStorage.UploadCreditNoteXmlAsync(
                fileName, xmlContent, cancellationToken);

            var transmission = await transmissionProvider.TransmitAsync(
                fileName, xmlContent, cancellationToken);
            if (!transmission.Success)
                return await FailAsync(
                    transmission.ErrorMessage ?? "La trasmissione della nota di credito allo SDI non è riuscita.");

            var transmittedAtUtc = DateTime.UtcNow;
            var marked = creditNote.MarkAsTransmitted(
                fileName, storageKey, transmission.TransmissionId, transmittedAtUtc);
            if (!marked.Success)
                return await FailAsync(marked.Error!);

            var transmissionId = transmission.TransmissionId ?? "non disponibile";
            await unitOfWork.AuditTrail.AddAsync(new DocumentAuditEntry(
                AuditDocumentTypes.CreditNote,
                creditNote.Id,
                AuditActions.CreditNoteTransmittedToSdi,
                $"Nota di credito trasmessa a SDI con file {fileName} e ID {transmissionId}",
                currentUser.UserId,
                currentUser.FullName ?? currentUser.Email,
                $"Fattura collegata {creditNote.RelatedInvoiceNumber ?? creditNote.InvoiceId.ToString()}",
                transmittedAtUtc));

            await unitOfWork.SaveChangesAsync();
            await unitOfWork.CommitAsync();
            transactionStarted = false;
            storageKey = null;
            return OperationResult<CreditNoteSummaryDto>.Ok(
                CreditNoteMapper.ToSummaryDto(creditNote));
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
                    await documentStorage.DeleteCreditNoteXmlAsync(storageKey, CancellationToken.None);
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
                return OperationResult<CreditNoteSummaryDto>.Fail(exception.Message);

            throw;
        }

        async Task<OperationResult<CreditNoteSummaryDto>> FailAsync(string error)
        {
            if (transactionStarted)
            {
                await unitOfWork.RollbackAsync();
                transactionStarted = false;
            }

            if (storageKey is not null)
            {
                await documentStorage.DeleteCreditNoteXmlAsync(storageKey, CancellationToken.None);
                storageKey = null;
            }

            return OperationResult<CreditNoteSummaryDto>.Fail(error);
        }
    }
}
