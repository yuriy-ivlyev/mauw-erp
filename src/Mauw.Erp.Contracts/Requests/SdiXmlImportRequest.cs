namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Request for importing SDI XML.
/// </summary>
public class SdiXmlImportRequest
{
    public string FilePath { get; set; } = "";
}