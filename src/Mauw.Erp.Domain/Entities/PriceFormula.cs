using Mauw.Erp.Domain.Accounting;
using Mauw.Erp.Domain.Pricing;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.Domain.Entities;

/// <summary>Formula prezzi globale e unica del catalogo, al netto di IVA.</summary>
public sealed class PriceFormula : Entity<Guid>
{
    public const int MaxUserIdLength = 200;
    private static readonly PriceFormulaCalculator Calculator = new();

    private readonly List<PriceFormulaStep> _steps = [];
    public decimal RoundingStep { get; private set; }
    public string? UpdatedByUserId { get; private set; }
    public IReadOnlyCollection<PriceFormulaStep> Steps => _steps.AsReadOnly();

    private PriceFormula() : base(Guid.NewGuid()) { }

    public static AccountingResult<PriceFormula> Create(PriceFormulaDefinition definition, string? userId) =>
        new PriceFormula().Update(definition, userId);

    public AccountingResult<PriceFormula> Update(PriceFormulaDefinition definition, string? userId)
    {
        var validation = Calculator.Validate(definition);
        if (!validation.Success)
            return AccountingResult<PriceFormula>.Fail(validation.Error);
        var user = string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();
        if (user?.Length > MaxUserIdLength)
            return AccountingResult<PriceFormula>.Fail(
                $"L'identificativo utente non puo superare {MaxUserIdLength} caratteri.");

        var normalized = validation.Data!;
        var replacements = normalized.Steps
            .Select((step, index) => new PriceFormulaStep(Id, index + 1, step))
            .ToList();
        RoundingStep = normalized.RoundingStep;
        UpdatedByUserId = user;
        _steps.Clear();
        _steps.AddRange(replacements);
        UpdateTimestamp();
        return AccountingResult<PriceFormula>.Ok(this);
    }

    public PriceFormulaDefinition ToDefinition() => new(
        _steps.OrderBy(step => step.Position)
            .Select(step => new PriceFormulaStepDefinition(step.Label, step.Percentage)).ToList().AsReadOnly(),
        RoundingStep);
}
