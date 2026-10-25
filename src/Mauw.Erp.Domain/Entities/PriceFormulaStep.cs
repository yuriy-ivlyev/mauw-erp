using Mauw.Erp.Domain.Pricing;

namespace Mauw.Erp.Domain.Entities;

public sealed class PriceFormulaStep : Entity<Guid>
{
    public Guid FormulaId { get; private set; }
    public int Position { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public decimal Percentage { get; private set; }

    private PriceFormulaStep() : base(Guid.NewGuid()) { }

    internal PriceFormulaStep(Guid formulaId, int position, PriceFormulaStepDefinition definition)
        : base(Guid.NewGuid())
    {
        FormulaId = formulaId;
        Position = position;
        Label = definition.Label;
        Percentage = definition.Percentage;
    }
}
