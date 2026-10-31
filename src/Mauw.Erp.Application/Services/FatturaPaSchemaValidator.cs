using System.Reflection;
using System.Xml;
using System.Xml.Schema;

namespace Mauw.Erp.Application.Services;

public static class FatturaPaSchemaValidator
{
    private const string FatturaPaNamespace = "http://ivaservizi.agenziaentrate.gov.it/docs/xsd/fatture/v1.2";
    private const string XmlDsigNamespace = "http://www.w3.org/2000/09/xmldsig#";

    public static IReadOnlyList<string> Validate(string xml)
    {
        var schemas = new XmlSchemaSet { XmlResolver = null };
        schemas.Add(XmlDsigNamespace, ReadResource("xmldsig-core-schema.stub.xsd"));
        schemas.Add(FatturaPaNamespace, ReadResource("Schema_VFPR12_v1.2.3.xsd"));
        var errors = new List<string>();
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            Schemas = schemas,
            ValidationType = ValidationType.Schema,
            XmlResolver = null
        };
        settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
        settings.ValidationEventHandler += (_, args) => errors.Add(args.Message);

        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            while (reader.Read())
            {
            }
        }
        catch (XmlException exception)
        {
            errors.Add(exception.Message);
        }

        return errors;
    }

    private static XmlReader ReadResource(string suffix)
    {
        var assembly = typeof(FatturaPaSchemaValidator).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(resource => resource.EndsWith(suffix, StringComparison.Ordinal));
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        return XmlReader.Create(assembly.GetManifestResourceStream(resourceName)!, settings);
    }
}
