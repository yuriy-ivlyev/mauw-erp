using System.Reflection;
using System.Xml;
using System.Xml.Schema;

namespace Mauw.Erp.UnitTests.Application;

/// <summary>Validates documents against the official FatturaPA 1.2.3 ordinary-invoice schema (embedded resource).</summary>
internal static class FatturaPaSchemaValidator
{
    private const string FatturaPaNamespace = "http://ivaservizi.agenziaentrate.gov.it/docs/xsd/fatture/v1.2";
    private const string XmlDsigNamespace = "http://www.w3.org/2000/09/xmldsig#";

    public static List<string> Validate(string xml)
    {
        var schemas = new XmlSchemaSet { XmlResolver = null };
        schemas.Add(XmlDsigNamespace, ReadResource("xmldsig-core-schema.stub.xsd"));
        schemas.Add(FatturaPaNamespace, ReadResource("Schema_VFPR12_v1.2.3.xsd"));
        var errors = new List<string>();
        var settings = new XmlReaderSettings { Schemas = schemas, ValidationType = ValidationType.Schema, XmlResolver = null };
        // Warnings include "no schema information for element": they must not go unnoticed.
        settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
        settings.ValidationEventHandler += (_, args) => errors.Add(args.Message);
        using var reader = XmlReader.Create(new StringReader(xml), settings);
        while (reader.Read())
        {
        }

        return errors;
    }

    private static XmlReader ReadResource(string suffix)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetManifestResourceNames().Single(resource => resource.EndsWith(suffix, StringComparison.Ordinal));
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
        return XmlReader.Create(assembly.GetManifestResourceStream(name)!, settings);
    }
}
