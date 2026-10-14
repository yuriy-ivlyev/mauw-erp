using System.Globalization;
using System.Xml.Linq;
using Mauw.Erp.Contracts;

namespace Mauw.Erp.Infrastructure;

public sealed class SdiXmlInvoiceParser
{
    public async Task<SdiImportPreviewDto> ParseAsync(Stream xmlStream, CancellationToken cancellationToken = default)
    {
        var document = await XDocument.LoadAsync(xmlStream, LoadOptions.None, cancellationToken);

        var cedentePrestatore = FindFirstDescendant(document.Root, "CedentePrestatore");
        var datiAnagrafici = FindFirstDescendant(cedentePrestatore, "DatiAnagrafici");
        var idFiscaleIva = FindFirstDescendant(datiAnagrafici, "IdFiscaleIVA");
        var anagrafica = FindFirstDescendant(datiAnagrafici, "Anagrafica");

        var lineItems = FindDescendants(document.Root, "DettaglioLinee")
            .Select(line =>
            {
                var codiceArticolo = FindFirstDescendant(line, "CodiceArticolo");

                return new SdiLineItemDto
                {
                    NumeroLinea = ParseInt(GetValue(line, "NumeroLinea")),
                    CodiceTipo = GetValue(codiceArticolo, "CodiceTipo"),
                    CodiceValore = GetValue(codiceArticolo, "CodiceValore"),
                    Descrizione = GetValue(line, "Descrizione"),
                    Quantita = ParseDecimal(GetValue(line, "Quantita")),
                    PrezzoUnitario = ParseDecimal(GetValue(line, "PrezzoUnitario")),
                    AliquotaIva = ParseDecimal(GetValue(line, "AliquotaIVA"))
                };
            })
            .ToArray();

        return new SdiImportPreviewDto
        {
            FornitoreDenominazione = GetValue(anagrafica, "Denominazione"),
            FornitoreIdPaese = GetValue(idFiscaleIva, "IdPaese"),
            FornitoreIdCodice = GetValue(idFiscaleIva, "IdCodice"),
            FornitoreCodiceFiscale = GetValue(datiAnagrafici, "CodiceFiscale"),
            LineItems = lineItems
        };
    }

    private static IEnumerable<XElement> FindDescendants(XElement? element, string localName)
    {
        if (element is null)
        {
            return Enumerable.Empty<XElement>();
        }

        return element.Descendants().Where(x => x.Name.LocalName == localName);
    }

    private static XElement? FindFirstDescendant(XElement? element, string localName)
    {
        return FindDescendants(element, localName).FirstOrDefault();
    }

    private static string? GetValue(XElement? parent, string localName)
    {
        return parent?
            .Elements()
            .FirstOrDefault(x => x.Name.LocalName == localName)?
            .Value;
    }

    private static int ParseInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0;
    }

    private static decimal ParseDecimal(string? value)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0m;
    }
}
