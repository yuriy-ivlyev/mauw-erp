using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Repositories;
using MediatR;

namespace Mauw.Erp.Application.Queries.CustomerStatements;

public sealed class GetCustomerStatementQueryHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<GetCustomerStatementQuery, OperationResult<CustomerStatementDto>>
{
    public async Task<OperationResult<CustomerStatementDto>> Handle(
        GetCustomerStatementQuery request,
        CancellationToken cancellationToken)
    {
        if (request.CustomerId == Guid.Empty)
            return OperationResult<CustomerStatementDto>.Fail("Cliente non valido.");

        var customer = await unitOfWork.Contacts.GetCustomerForStatementAsync(
            request.CustomerId, cancellationToken);
        if (customer is null)
            return OperationResult<CustomerStatementDto>.Fail("Cliente non trovato.");

        var invoices = await unitOfWork.Invoices.GetConfirmedForStatementByContactIdAsync(
            request.CustomerId, cancellationToken);
        var creditNotes = await unitOfWork.CreditNotes.GetForStatementByContactIdAsync(
            request.CustomerId, cancellationToken);
        var movements = new List<StatementMovementDto>(
            invoices.Count + creditNotes.Count + invoices.Sum(invoice => invoice.Payments.Count));

        foreach (var invoice in invoices)
        {
            movements.Add(new StatementMovementDto(
                ToUtc(invoice.ConfirmedAt!.Value),
                "Invoice",
                invoice.Id,
                invoice.InvoiceNumber,
                $"Fattura di vendita {invoice.InvoiceNumber}",
                invoice.GrossTotal,
                0.00m,
                0.00m,
                null,
                null,
                invoice.SdiStatus.ToString()));

            foreach (var payment in invoice.Payments)
            {
                var paymentMethod = NormalizePaymentMethod(payment.PaymentMethod);
                var isCreditNoteOffset = paymentMethod == nameof(PaymentMethod.CreditNoteOffset);
                var sourceCreditNote = payment.SourceCreditNote;
                var description = isCreditNoteOffset
                    ? $"Compensazione da NC {sourceCreditNote?.CreditNoteNumber ?? "non specificata"}"
                    : BuildPaymentDescription(paymentMethod, payment.TransactionDetails);

                movements.Add(new StatementMovementDto(
                    ToUtc(payment.PaymentDate),
                    "Payment",
                    invoice.Id,
                    invoice.InvoiceNumber,
                    description,
                    0.00m,
                    payment.Amount,
                    0.00m,
                    paymentMethod,
                    sourceCreditNote?.CreditNoteNumber,
                    null,
                    payment.SourceCreditNoteId));
            }
        }

        // Allocated credit is already represented by the linked CreditNoteOffset payment.
        foreach (var creditNote in creditNotes)
        {
            movements.Add(new StatementMovementDto(
                ToUtc(creditNote.CreditNoteDate),
                "CreditNote",
                creditNote.Id,
                creditNote.CreditNoteNumber,
                $"Nota di credito {creditNote.CreditNoteNumber}",
                0.00m,
                creditNote.RemainingCredit,
                0.00m,
                null,
                null,
                creditNote.SdiStatus.ToString()));
        }

        var orderedMovements = movements
            .OrderBy(movement => movement.DateUtc)
            .ThenBy(MovementOrder)
            .ThenBy(movement => movement.DocumentNumber, StringComparer.Ordinal)
            .ThenBy(movement => movement.PaymentMethod, StringComparer.Ordinal)
            .ThenBy(movement => movement.SourceCreditNoteNumber, StringComparer.Ordinal)
            .ThenBy(movement => movement.DocumentId)
            .ToArray();

        decimal runningBalance = 0.00m;
        for (var index = 0; index < orderedMovements.Length; index++)
        {
            runningBalance += orderedMovements[index].Debit - orderedMovements[index].Credit;
            orderedMovements[index] = orderedMovements[index] with { RunningBalance = runningBalance };
        }

        var totalInvoiced = orderedMovements
            .Where(movement => movement.Type == "Invoice")
            .Sum(movement => movement.Debit);
        var totalCredited = orderedMovements
            .Where(movement => movement.Type == "CreditNote")
            .Sum(movement => movement.Credit);
        var totalPaid = orderedMovements
            .Where(movement => movement.Type == "Payment")
            .Sum(movement => movement.Credit);
        var availableCredit = creditNotes.Sum(note => note.RemainingCredit);

        return OperationResult<CustomerStatementDto>.Ok(new CustomerStatementDto(
            customer.Id,
            customer.BusinessName,
            totalInvoiced,
            totalCredited,
            totalPaid,
            totalInvoiced - totalCredited - totalPaid,
            availableCredit,
            orderedMovements.ToList()));
    }

    private static int MovementOrder(StatementMovementDto movement) =>
        movement.Type switch
        {
            "Invoice" => 0,
            "CreditNote" => 1,
            _ => 2
        };

    private static string? NormalizePaymentMethod(string? paymentMethod)
    {
        if (string.IsNullOrWhiteSpace(paymentMethod))
            return null;

        var method = paymentMethod.Trim();
        if (string.Equals(method, "Bonifico bancario", StringComparison.OrdinalIgnoreCase))
            return nameof(PaymentMethod.BankTransfer);
        if (string.Equals(method, "Contanti", StringComparison.OrdinalIgnoreCase))
            return nameof(PaymentMethod.Cash);
        if (string.Equals(method, "Carta", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(method, "POS", StringComparison.OrdinalIgnoreCase))
            return nameof(PaymentMethod.Pos);
        if (string.Equals(method, "Assegno", StringComparison.OrdinalIgnoreCase))
            return nameof(PaymentMethod.Check);
        if (string.Equals(method, "Altro", StringComparison.OrdinalIgnoreCase))
            return nameof(PaymentMethod.Other);
        if (string.Equals(
                method,
                nameof(PaymentMethod.CreditNoteOffset),
                StringComparison.OrdinalIgnoreCase))
            return nameof(PaymentMethod.CreditNoteOffset);

        return method;
    }

    private static string BuildPaymentDescription(string? paymentMethod, string? transactionDetails)
    {
        var method = paymentMethod ?? "Metodo non specificato";
        return string.IsNullOrWhiteSpace(transactionDetails)
            ? $"Incasso {method}"
            : $"Incasso {method} (rif: {transactionDetails.Trim()})";
    }

    private static DateTime ToUtc(DateTime dateTime) =>
        dateTime.Kind switch
        {
            DateTimeKind.Utc => dateTime,
            DateTimeKind.Local => dateTime.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
        };
}
