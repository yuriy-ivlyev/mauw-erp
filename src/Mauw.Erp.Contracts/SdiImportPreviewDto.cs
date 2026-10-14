namespace Mauw.Erp.Contracts;

public sealed class SdiImportPreviewDto
{
    public string? FornitoreDenominazione { get; init; }

    public string? FornitoreIdPaese { get; init; }

    public string? FornitoreIdCodice { get; init; }

    public string? FornitoreCodiceFiscale { get; init; }

    public IReadOnlyList<SdiLineItemDto> LineItems { get; init; } = [];
}
