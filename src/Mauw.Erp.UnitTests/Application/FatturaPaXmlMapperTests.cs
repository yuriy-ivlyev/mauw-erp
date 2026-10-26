using System.Xml.Linq;
using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Contracts.Dtos.Pdf;

namespace Mauw.Erp.UnitTests.Application;

public class FatturaPaXmlMapperTests
{
    private static readonly XNamespace FatturaPaNamespace =
        "http://ivaservizi.agenziaentrate.gov.it/docs/xsd/fatture/v1.2";

    [Fact]
    public void BusinessRecipientCodeExcludesPecFromTransmissionAddress()
    {
        var elements = FatturaPaXmlMapper.BuildRecipientAddressElements(
            "NS9Q9ED", "cliente@example.test", FatturaPaNamespace);

        var code = Assert.Single(elements);
        Assert.Equal(FatturaPaNamespace + "CodiceDestinatario", code.Name);
        Assert.Equal("NS9Q9ED", code.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0000000")]
    public void DefaultRecipientCodeIncludesPecOnlyWhenAvailable(string? code)
    {
        var elements = FatturaPaXmlMapper.BuildRecipientAddressElements(
            code, " cliente@example.test ", FatturaPaNamespace);

        Assert.Collection(elements,
            element =>
            {
                Assert.Equal(FatturaPaNamespace + "CodiceDestinatario", element.Name);
                Assert.Equal("0000000", element.Value);
            },
            element =>
            {
                Assert.Equal(FatturaPaNamespace + "PECDestinatario", element.Name);
                Assert.Equal("cliente@example.test", element.Value);
            });
    }

    [Fact]
    public void DefaultRecipientCodeWithoutPecContainsOnlyRequiredCode()
    {
        var elements = FatturaPaXmlMapper.BuildRecipientAddressElements(" ", null);

        var code = Assert.Single(elements);
        Assert.Equal("CodiceDestinatario", code.Name.LocalName);
        Assert.Equal("0000000", code.Value);
    }

    [Fact]
    public void PaymentXmlContainsMethodDueDateAndAmount()
    {
        var result = FatturaPaXmlMapper.BuildDatiPagamento(
            new InvoicePdfPaymentInfo(
                "A vista", "MP01", new DateOnly(2026, 10, 25), null, null, null, 122.20m),
            FatturaPaNamespace);

        Assert.True(result.Success, result.Error);
        Assert.Equal("TP02", result.Data!.Element(FatturaPaNamespace + "CondizioniPagamento")!.Value);
        var details = result.Data.Element(FatturaPaNamespace + "DettaglioPagamento")!;
        Assert.Equal("MP01", details.Element(FatturaPaNamespace + "ModalitaPagamento")!.Value);
        Assert.Equal("2026-10-25", details.Element(FatturaPaNamespace + "DataScadenzaPagamento")!.Value);
        Assert.Equal("122.20", details.Element(FatturaPaNamespace + "ImportoPagamento")!.Value);
    }

    [Fact]
    public void PaymentXmlRejectsMissingOrNonMonetaryAmount()
    {
        var missingAmount = FatturaPaXmlMapper.BuildDatiPagamento(
            new InvoicePdfPaymentInfo("A vista", "MP01", null, null, null, null));
        var excessPrecision = FatturaPaXmlMapper.BuildDatiPagamento(
            new InvoicePdfPaymentInfo("A vista", "MP01", null, null, null, null, 1.001m));

        Assert.False(missingAmount.Success);
        Assert.False(excessPrecision.Success);
    }
}
