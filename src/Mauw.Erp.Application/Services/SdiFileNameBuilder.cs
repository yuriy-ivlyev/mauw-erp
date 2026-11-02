using System.Xml;
using System.Xml.Linq;

namespace Mauw.Erp.Application.Services;

public static class SdiFileNameBuilder
{
    public static OperationResult<string> Build(string xml)
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
