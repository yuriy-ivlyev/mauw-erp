using Mauw.Erp.Domain.Accounting;
using Mauw.Erp.Domain.Pricing;

namespace Mauw.Erp.Domain.Entities;

public sealed class PriceList : Entity<Guid>
{
    private readonly List<PriceListRule> _rules = [];
    public string Name { get; private set; } = string.Empty;
    public string Currency { get; private set; } = "EUR";
    public bool IsActive { get; private set; }
    public IReadOnlyCollection<PriceListRule> Rules => _rules.AsReadOnly();

    private PriceList() : base(Guid.NewGuid()) { }

    public static AccountingResult<PriceList> Create(
        string name, string currency, bool isActive, IReadOnlyList<PriceRuleDefinition>? rules)
    {
        var list = new PriceList();
        return list.Update(name, currency, isActive, rules);
    }

    public AccountingResult<PriceList> Update(
        string name, string currency, bool isActive, IReadOnlyList<PriceRuleDefinition>? rules)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            return AccountingResult<PriceList>.Fail("Il nome del listino e obbligatorio (massimo 200 caratteri).");
        if (currency != "EUR")
            return AccountingResult<PriceList>.Fail("La valuta supportata e EUR.");
        if (rules is null)
            return AccountingResult<PriceList>.Fail("Specificare le regole del listino.");
        var ids = new HashSet<Guid>();
        foreach (var rule in rules)
        {
            if (rule is null || !Enum.IsDefined(rule.CalculationType) ||
                rule.Value < 0m || rule.Value > 99999999999999.99999999m ||
                decimal.Round(rule.Value, 8, MidpointRounding.AwayFromZero) != rule.Value ||
                rule.MinimumQuantity <= 0m || rule.MinimumQuantity > 99999999999999.99999999m ||
                decimal.Round(rule.MinimumQuantity, 8, MidpointRounding.AwayFromZero) != rule.MinimumQuantity ||
                rule.MinimumQuantity != decimal.Truncate(rule.MinimumQuantity))
                return AccountingResult<PriceList>.Fail("Tipo, valore o quantita minima della regola non validi.");
            if (rule.CalculationType == PriceCalculationType.PercentageDiscount && rule.Value > 100m)
                return AccountingResult<PriceList>.Fail("Lo sconto deve essere compreso tra 0 e 100.");
            if (new[] { rule.VariantId, rule.ProductId, rule.CategoryId }.Count(id => id.HasValue) > 1 ||
                rule.VariantId == Guid.Empty || rule.ProductId == Guid.Empty || rule.CategoryId == Guid.Empty)
                return AccountingResult<PriceList>.Fail("Specificare un solo bersaglio oppure nessuno per una regola globale.");
            if (rule.ValidFrom.HasValue && rule.ValidTo.HasValue && rule.ValidFrom > rule.ValidTo)
                return AccountingResult<PriceList>.Fail("La fine validita non puo precedere l'inizio.");
            if (rule.Id == Guid.Empty || (rule.Id.HasValue && !ids.Add(rule.Id.Value)))
                return AccountingResult<PriceList>.Fail("Identificativo regola vuoto o duplicato.");
            if (rule.Description?.Trim().Length > 200)
                return AccountingResult<PriceList>.Fail("La causale della regola non puo superare 200 caratteri.");
        }

        var existing = _rules.ToDictionary(rule => rule.Id);
        var replacements = rules.Select(rule =>
        {
            if (rule.Id.HasValue && existing.TryGetValue(rule.Id.Value, out var current))
            {
                current.Apply(rule);
                return current;
            }
            return new PriceListRule(Id, rule);
        }).ToList();
        Name = name.Trim();
        Currency = currency;
        IsActive = isActive;
        _rules.Clear();
        _rules.AddRange(replacements);
        UpdateTimestamp();
        return AccountingResult<PriceList>.Ok(this);
    }

    public void SetActivation(bool isActive)
    {
        IsActive = isActive;
        UpdateTimestamp();
    }
}
