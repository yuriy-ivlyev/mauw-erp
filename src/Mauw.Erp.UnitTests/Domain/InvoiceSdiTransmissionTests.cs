using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.UnitTests.Domain;

public sealed class InvoiceSdiTransmissionTests
{
    [Fact]
    public void MarkAsTransmittedStoresArchiveMetadataAndFreezesInvoice()
    {
        var invoice = ConfirmedInvoice();
        var transmittedAtUtc = DateTime.UtcNow;

        var result = invoice.MarkAsTransmitted(
            "IT01234567890_0261000001.xml",
            "invoices/2026/10/IT01234567890_0261000001.xml",
            "SDI-123",
            transmittedAtUtc);

        Assert.True(result.Success, result.Error);
        Assert.Equal("IT01234567890_0261000001.xml", invoice.SdiFileName);
        Assert.Equal("invoices/2026/10/IT01234567890_0261000001.xml", invoice.SdiStorageKey);
        Assert.Equal("SDI-123", invoice.SdiTransmissionId);
        Assert.Equal(transmittedAtUtc, invoice.SdiTransmittedAtUtc);
        Assert.Equal(transmittedAtUtc, invoice.SdiSubmittedAt);
        Assert.Equal(SdiStatus.Transmitted, invoice.SdiStatus);
        Assert.False(invoice.CanBeAdjustedBeforeSdi);
        Assert.False(invoice.MarkAsTransmitted("duplicate.xml", "duplicate", null, transmittedAtUtc).Success);

        Assert.True(invoice.MarkSdiAccepted().Success);
        Assert.False(invoice.MarkAsTransmitted("accepted.xml", "accepted", null, transmittedAtUtc).Success);
    }

    [Fact]
    public void MarkAsTransmittedAllowsAPendingInvoice()
    {
        var invoice = ConfirmedInvoice();
        Assert.True(invoice.MarkSdiPending().Success);

        var result = invoice.MarkAsTransmitted(
            "IT01234567890_0261000001.xml",
            "invoices/2026/10/IT01234567890_0261000001.xml",
            null,
            DateTime.UtcNow);

        Assert.True(result.Success, result.Error);
        Assert.Null(invoice.SdiTransmissionId);
        Assert.False(invoice.CanBeAdjustedBeforeSdi);
    }

    [Fact]
    public void MarkAsTransmittedRejectsInvalidMetadataWithoutMutatingInvoice()
    {
        var invoice = ConfirmedInvoice();

        var result = invoice.MarkAsTransmitted(
            new string('x', 51),
            "invoices/2026/10/file.xml",
            "SDI-123",
            DateTime.UtcNow);

        Assert.False(result.Success);
        Assert.Null(invoice.SdiFileName);
        Assert.Null(invoice.SdiStorageKey);
        Assert.Null(invoice.SdiTransmissionId);
        Assert.Null(invoice.SdiTransmittedAtUtc);
    }

    private static Invoice ConfirmedInvoice()
    {
        var calculator = new ItalianAccountingCalculator();
        var invoice = new Invoice(Guid.NewGuid(), "FAT-202610-00001");
        Assert.True(invoice.AddItem(
            new InvoiceItem(invoice.Id, Guid.NewGuid(), 1m, 10m, 22m), calculator).Success);
        Assert.True(invoice.Confirm(calculator).Success);
        return invoice;
    }
}
