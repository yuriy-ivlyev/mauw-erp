using System.Text;
using Mauw.Erp.Infrastructure;

namespace Mauw.Erp.UnitTests;

public sealed class SdiXmlInvoiceParserTests
{
    [Fact]
    public async Task ParseAsync_ExtractsSupplierAndLineItems_WithExpectedNumericValues()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <p:FatturaElettronica versione="FPR12" xmlns:p="http://ivaservizi.agenziaentrate.gov.it/docs/xsd/fatture/v1.2">
              <FatturaElettronicaHeader>
                <CedentePrestatore>
                  <DatiAnagrafici>
                    <IdFiscaleIVA>
                      <IdPaese>IT</IdPaese>
                      <IdCodice>12345678901</IdCodice>
                    </IdFiscaleIVA>
                    <CodiceFiscale>12345678901</CodiceFiscale>
                    <Anagrafica>
                      <Denominazione>Fornitore Demo SRL</Denominazione>
                    </Anagrafica>
                  </DatiAnagrafici>
                </CedentePrestatore>
              </FatturaElettronicaHeader>
              <FatturaElettronicaBody>
                <DatiBeniServizi>
                  <DettaglioLinee>
                    <NumeroLinea>1</NumeroLinea>
                    <CodiceArticolo>
                      <CodiceTipo>SKU</CodiceTipo>
                      <CodiceValore>TSHIRT-RED-M</CodiceValore>
                    </CodiceArticolo>
                    <Descrizione>T-Shirt Rossa M</Descrizione>
                    <Quantita>10.50</Quantita>
                    <PrezzoUnitario>12.99</PrezzoUnitario>
                    <AliquotaIVA>22.00</AliquotaIVA>
                  </DettaglioLinee>
                </DatiBeniServizi>
              </FatturaElettronicaBody>
            </p:FatturaElettronica>
            """;

        var parser = new SdiXmlInvoiceParser();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        var preview = await parser.ParseAsync(stream);

        Assert.Equal("Fornitore Demo SRL", preview.FornitoreDenominazione);
        Assert.Equal("IT", preview.FornitoreIdPaese);
        Assert.Equal("12345678901", preview.FornitoreIdCodice);
        Assert.Equal("12345678901", preview.FornitoreCodiceFiscale);

        var line = Assert.Single(preview.LineItems);
        Assert.Equal(1, line.NumeroLinea);
        Assert.Equal("SKU", line.CodiceTipo);
        Assert.Equal("TSHIRT-RED-M", line.CodiceValore);
        Assert.Equal("T-Shirt Rossa M", line.Descrizione);
        Assert.Equal(10.50m, line.Quantita);
        Assert.Equal(12.99m, line.PrezzoUnitario);
        Assert.Equal(22.00m, line.AliquotaIva);
    }
}
