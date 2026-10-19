using System.Text.Json.Serialization;
using Mauw.Erp.Domain.Pricing;

namespace Mauw.Erp.Contracts.Pricing;

/// <summary>Regola condizionale di un listino EUR.</summary>
public sealed record PriceRuleRequest
{
    public Guid? Id { get; init; }
    [JsonConverter(typeof(JsonStringEnumConverter<PriceCalculationType>))]
    public PriceCalculationType CalculationType { get; init; }
    public decimal Value { get; init; }
    public Guid? VariantId { get; init; }
    public Guid? ProductId { get; init; }
    public Guid? CategoryId { get; init; }
    public decimal MinimumQuantity { get; init; } = 1m;
    public DateTimeOffset? ValidFrom { get; init; }
    public DateTimeOffset? ValidTo { get; init; }
    public int Priority { get; init; }
}

/// <summary>Configurazione completa di un listino e delle sue regole.</summary>
public sealed record SavePriceListRequest
{
    public string Name { get; init; } = string.Empty;
    public string Currency { get; init; } = "EUR";
    public bool IsActive { get; init; } = true;
    public IReadOnlyList<PriceRuleRequest> Rules { get; init; } = [];
}

/// <summary>Stato di attivazione richiesto per il listino.</summary>
public sealed record PriceListActivationRequest(bool IsActive);

/// <summary>Listino con regole condizionali, senza navigazioni di persistenza.</summary>
public sealed record PriceListResponse(
    Guid Id, string Name, string Currency, bool IsActive, IReadOnlyList<PriceRuleRequest> Rules);

/// <summary>Variante e quantita fiscale per la quotazione.</summary>
public sealed record ResolvePriceLineRequest(Guid VariantId, decimal Quantity);

/// <summary>Quotazione massiva: il listino e il catalogo vengono caricati prima del calcolo.</summary>
public sealed record ResolvePricesRequest
{
    public Guid? ContactId { get; init; }
    public Guid? PriceListId { get; init; }
    public DateTimeOffset At { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<ResolvePriceLineRequest> Lines { get; init; } = [];
}

/// <summary>Prezzo unitario quotato; non contiene imposte di riga.</summary>
public sealed record ResolvedPriceLineResponse(
    Guid VariantId, Guid? PriceListId, Guid? RuleId,
    decimal ListUnitPrice, decimal DiscountPercent, decimal AgreedUnitNetPrice, decimal VatRate);

/// <summary>Quotazione ordinata come le righe della richiesta.</summary>
public sealed record ResolvedPricesResponse(Guid? PriceListId, IReadOnlyList<ResolvedPriceLineResponse> Lines);

/// <summary>Snapshot informativo congelato della politica di prezzo applicata alla riga.</summary>
public sealed record PricingSnapshotDto(
    Guid? PriceListId, Guid? RuleId, decimal ListUnitPrice, decimal? AgreedUnitNetPrice,
    bool IsManualOverride, string? OverrideReason, bool IsCustomerReturn, Guid? SourceLineId);
