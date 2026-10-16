namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// Nazione ISO 3166-1 alpha-2.
/// Entità di lookup (non estende Entity&lt;TId&gt; per evitare campi audit non necessari).
/// </summary>
public class Country
{
    /// <summary>Codice ISO 3166-1 alpha-2 (es. "IT", "DE", "US"). PK.</summary>
    public string IsoCode { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsEuMember { get; set; }
    /// <summary>Ordine di visualizzazione (paesi prioritari prima).</summary>
    public int DisplayOrder { get; set; }
}
