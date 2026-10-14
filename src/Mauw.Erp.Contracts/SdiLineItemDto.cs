namespace Mauw.Erp.Contracts;

public sealed class SdiLineItemDto
{
    public int NumeroLinea { get; init; }

    public string? CodiceTipo { get; init; }

    public string? CodiceValore { get; init; }

    public string? Descrizione { get; init; }

    public decimal Quantita { get; init; }

    public decimal PrezzoUnitario { get; init; }

    public decimal AliquotaIva { get; init; }
}
