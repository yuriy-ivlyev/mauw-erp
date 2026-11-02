using Mauw.Erp.Application.Queries.CustomerStatements;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Infrastructure.DbContext;
using Mauw.Erp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Mauw.Erp.UnitTests.Application;

[Collection(PostgreSqlTestCollection.Name)]
public sealed class CustomerStatementPostgreSqlTests(PostgreSqlContainerFixture postgres)
{
    [Fact]
    public async Task StatementReconcilesInvoicesPaymentsCreditNotesAndAllocations()
    {
        var database = postgres.CreateDatabase();
        Guid customerId;

        await using (var seedDb = database.CreateDbContext())
        {
            var contact = new Contact($"Cliente {Guid.NewGuid():N}");
            SetId(contact);
            contact.EnableAsCustomer();

            var invoice1 = CreateConfirmedInvoice(
                contact.Id, "FAT-202601-00001", 1000.00m, UtcDate(1, 9));
            invoice1.Contact = contact;
            Assert.True(invoice1.RecordPayment(1000.00m).Success);

            var creditNote = new CreditNote(invoice1.Id, "NC-202601-00001")
            {
                CreditNoteDate = UtcDate(3, 9),
                GrossTotal = 60.00m,
                Invoice = invoice1
            };
            invoice1.CreditNotes.Add(creditNote);
            Assert.True(creditNote.AllocateCredit(60.00m).Success);

            var invoice2 = CreateConfirmedInvoice(
                contact.Id, "FAT-202601-00002", 500.00m, UtcDate(4, 9));
            invoice2.Contact = contact;
            Assert.True(invoice2.RecordPayment(500.00m).Success);

            invoice1.Payments.Add(new Payment(
                invoice1.Id, 1000.00m, "POS", paymentDate: UtcDate(2, 9)));
            invoice2.Payments.Add(new Payment(
                invoice2.Id,
                60.00m,
                nameof(PaymentMethod.CreditNoteOffset),
                paymentDate: UtcDate(5, 9),
                sourceCreditNoteId: creditNote.Id,
                sourceCreditNote: creditNote));
            invoice2.Payments.Add(new Payment(
                invoice2.Id, 440.00m, "Cash", paymentDate: UtcDate(5, 10)));

            seedDb.AddRange(contact, invoice1, invoice2);
            await seedDb.SaveChangesAsync();
            customerId = contact.Id;
        }

        await using var queryDb = database.CreateDbContext();
        var unitOfWork = new UnitOfWork(queryDb, new MemoryCache(new MemoryCacheOptions()));
        var result = await new GetCustomerStatementQueryHandler(unitOfWork).Handle(
            new GetCustomerStatementQuery(customerId),
            CancellationToken.None);

        Assert.True(result.Success, result.Error);
        var statement = result.Data!;
        Assert.Equal(6, statement.Movements.Count);
        Assert.Equal(
            ["Invoice", "Payment", "CreditNote", "Invoice", "Payment", "Payment"],
            statement.Movements.Select(movement => movement.Type));
        Assert.Equal(1500.00m, statement.TotalInvoiced);
        Assert.Equal(0.00m, statement.TotalCredited);
        Assert.Equal(1500.00m, statement.TotalPaid);
        Assert.Equal(0.00m, statement.NetBalance);
        Assert.Equal(0.00m, statement.AvailableCreditNotesAmount);
        Assert.Equal(0.00m, statement.Movements[^1].RunningBalance);

        var offset = Assert.Single(statement.Movements,
            movement => movement.PaymentMethod == nameof(PaymentMethod.CreditNoteOffset));
        Assert.Equal("NC-202601-00001", offset.SourceCreditNoteNumber);
        Assert.Equal(60.00m, offset.Credit);
        Assert.Contains(statement.Movements, movement => movement.PaymentMethod == "Pos");
        Assert.Contains(statement.Movements, movement => movement.PaymentMethod == "Cash");

        var creditNoteMovement = Assert.Single(statement.Movements,
            movement => movement.Type == "CreditNote");
        Assert.Equal(0.00m, creditNoteMovement.Credit);
    }

    private static Invoice CreateConfirmedInvoice(
        Guid contactId,
        string invoiceNumber,
        decimal grossTotal,
        DateTime confirmedAt)
    {
        var invoice = new Invoice(contactId, invoiceNumber)
        {
            InvoiceDate = confirmedAt,
            TaxableTotal = grossTotal,
            VatTotal = 0.00m,
            GrossTotal = grossTotal
        };
        SetId(invoice);
        typeof(Invoice).GetProperty(nameof(Invoice.ConfirmedAt))!
            .SetValue(invoice, confirmedAt);
        return invoice;
    }

    private static DateTime UtcDate(int day, int hour) =>
        new(2026, 1, day, hour, 0, 0, DateTimeKind.Utc);

    private static void SetId(Entity<Guid> entity) =>
        typeof(Entity<Guid>).GetProperty(nameof(Entity<Guid>.Id))!
            .SetValue(entity, Guid.NewGuid());
}
