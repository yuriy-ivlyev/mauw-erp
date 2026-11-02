using System.Data;
using System.Globalization;
using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Exceptions;
using Mauw.Erp.Domain.Repositories;
using MediatR;

namespace Mauw.Erp.Application.Commands.CreditNotes;

public sealed record CreateCreditNoteCommand(CreateCreditNoteRequest Request)
    : IRequest<OperationResult<CreditNoteIssuedDto>>;

public sealed class CreateCreditNoteCommandHandler(
    IUnitOfWork unitOfWork,
    CreditNoteService creditNoteService,
    IInvoiceService invoiceService,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateCreditNoteCommand, OperationResult<CreditNoteIssuedDto>>
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public async Task<OperationResult<CreditNoteIssuedDto>> Handle(
        CreateCreditNoteCommand command,
        CancellationToken cancellationToken)
    {
        var requestError = CreditNoteService.ValidateRequest(command.Request);
        if (requestError is not null)
            return OperationResult<CreditNoteIssuedDto>.Fail(requestError);

        var transactionStarted = false;
        try
        {
            await unitOfWork.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            transactionStarted = true;
            await unitOfWork.CreditNotes.AcquireInvoiceCreditNoteLockAsync(command.Request.InvoiceId);
            cancellationToken.ThrowIfCancellationRequested();

            var invoice = await unitOfWork.Invoices.GetByIdAsync(command.Request.InvoiceId);
            if (invoice is null)
                return await FailAsync("Fattura non trovata.");
            if (invoice.SdiStatus is not (SdiStatus.Transmitted or SdiStatus.Accepted))
                return await FailAsync(
                    "La nota di credito può essere emessa solo per fatture trasmesse o accettate dallo SDI.");

            var warehouseError = await creditNoteService.ValidateWarehouseAsync(
                command.Request, cancellationToken);
            if (warehouseError is not null)
                return await FailAsync(warehouseError);

            var prepared = creditNoteService.Prepare(invoice, command.Request);
            if (!prepared.Success)
                return await FailAsync(prepared.Error!);
            if (prepared.Data!.Lines.Count == 0 || prepared.Data.Document.TotalGross <= 0m)
                return await FailAsync("Selezionare almeno una riga con un importo positivo da stornare.");

            var now = DateTime.UtcNow;
            var prefix = $"NC-{now.ToString("yyyyMM", Invariant)}-";
            await unitOfWork.CreditNotes.AcquireCreditNoteNumberLockAsync(prefix);
            var number = await AllocateNextCreditNoteNumberAsync(prefix);
            if (!number.Success)
                return await FailAsync(number.Error!);

            var note = new CreditNote(
                invoice,
                number.Data!,
                command.Request.Reason,
                command.Request.ReturnToWarehouse,
                command.Request.ReturnToWarehouse ? command.Request.WarehouseId : null,
                now)
            {
                TaxableTotal = prepared.Data.Document.TotalTaxable,
                VatTotal = prepared.Data.Document.TotalVat,
                GrossTotal = prepared.Data.Document.TotalGross
            };

            foreach (var line in prepared.Data.Lines)
            {
                var source = line.Source.InvoiceItem;
                note.Items.Add(new CreditNoteItem(
                    note,
                    line.Source.InvoiceItemId,
                    source?.VariantId,
                    line.Source.Description,
                    line.Source.IsShippingCharge,
                    line.Calculation.Quantity,
                    line.Calculation.UnitNetPrice,
                    line.Calculation.LineNetTotal,
                    line.Calculation.VatRate,
                    line.Calculation.NatureCode,
                    line.Calculation.DiscountPercent,
                    source,
                    source?.Variant));
            }

            await unitOfWork.CreditNotes.AddAsync(note);
            if (note.ReturnToWarehouse)
            {
                foreach (var item in note.Items.Where(item => !item.IsShippingCharge))
                {
                    var movement = StockMovement.FromCreditNote(
                        note, item, note.WarehouseId!.Value, currentUser.UserId);
                    await unitOfWork.Stock.RecordMovementAsync(movement, item.Quantity);
                }
            }

            var reasonLabel = CreditNoteReasons.GetLabel(note.Reason);
            await unitOfWork.AuditTrail.AddAsync(new DocumentAuditEntry(
                AuditDocumentTypes.Invoice,
                invoice.Id,
                AuditActions.CreditNoteIssued,
                $"Emessa nota di credito {note.CreditNoteNumber}",
                currentUser.UserId,
                currentUser.FullName ?? currentUser.Email,
                $"Nota {note.CreditNoteNumber}; causale {reasonLabel} ({note.Reason}); " +
                $"importo {note.GrossTotal.ToString("0.00", Invariant)} EUR."));

            cancellationToken.ThrowIfCancellationRequested();
            await unitOfWork.SaveChangesAsync();
            await unitOfWork.CommitAsync();
            transactionStarted = false;

            var pdf = await invoiceService.GetInvoicePdfAsync(note.Id);
            var issued = new CreditNoteIssuedDto(
                CreditNoteMapper.ToSummaryDto(note),
                pdf.Success ? pdf.Data : null,
                pdf.Success ? null : pdf.Error ?? "Generazione PDF non riuscita.");
            return OperationResult<CreditNoteIssuedDto>.Ok(issued);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentException or DomainException or
                ConcurrencyException or OverflowException)
        {
            if (transactionStarted)
                await unitOfWork.RollbackAsync();
            return OperationResult<CreditNoteIssuedDto>.Fail(exception.Message);
        }
        catch
        {
            if (transactionStarted)
                await unitOfWork.RollbackAsync();
            throw;
        }

        async Task<OperationResult<CreditNoteIssuedDto>> FailAsync(string error)
        {
            if (transactionStarted)
            {
                await unitOfWork.RollbackAsync();
                transactionStarted = false;
            }

            return OperationResult<CreditNoteIssuedDto>.Fail(error);
        }
    }

    private async Task<OperationResult<string>> AllocateNextCreditNoteNumberAsync(string prefix)
    {
        var existingNumbers = await unitOfWork.CreditNotes.GetCreditNoteNumbersByPrefixAsync(prefix);
        var sequences = new List<int>(existingNumbers.Count);
        foreach (var creditNoteNumber in existingNumbers)
        {
            if (!creditNoteNumber.StartsWith(prefix, StringComparison.Ordinal) ||
                creditNoteNumber.Length != prefix.Length + 5 ||
                !int.TryParse(creditNoteNumber.AsSpan(prefix.Length), NumberStyles.None,
                    Invariant, out var sequence))
                return OperationResult<string>.Fail(
                    $"La numerazione delle note di credito del periodo {prefix} contiene un numero non valido.");
            sequences.Add(sequence);
        }

        sequences.Sort();
        for (var index = 0; index < sequences.Count; index++)
        {
            if (sequences[index] != index + 1)
                return OperationResult<string>.Fail(
                    $"La numerazione delle note di credito del periodo {prefix} non è contigua.");
        }

        if (sequences.Count >= 99999)
            return OperationResult<string>.Fail(
                $"La numerazione delle note di credito del periodo {prefix} ha esaurito i progressivi.");

        return OperationResult<string>.Ok(
            $"{prefix}{(sequences.Count + 1).ToString("D5", Invariant)}");
    }

}
