using Mauw.Erp.Domain.Enums;

namespace Mauw.Erp.Domain.Entities;

public sealed class PayableDocumentLine : Entity<Guid>
{
    public Guid PayableDocumentId { get; private set; }
    public PayableDocument PayableDocument { get; private set; } = null!;
    public string Description { get; private set; } = string.Empty;
    public PayableExpenseCategory Category { get; private set; }
    public string? CostCenter { get; private set; }
    public string? Project { get; private set; }
    public decimal TaxableAmount { get; private set; }
    public decimal? VatRate { get; private set; }
    public string? NatureCode { get; private set; }
    public decimal VatAmount { get; private set; }
    public decimal GrossAmount { get; private set; }

    public PayableDocumentLine(
        Guid payableDocumentId,
        string description,
        PayableExpenseCategory category,
        decimal taxableAmount,
        decimal? vatRate,
        string? natureCode,
        decimal vatAmount,
        decimal grossAmount,
        string? costCenter,
        string? project) : base()
    {
        PayableDocumentId = payableDocumentId;
        Description = description.Trim();
        Category = category;
        TaxableAmount = taxableAmount;
        VatRate = vatRate;
        NatureCode = string.IsNullOrWhiteSpace(natureCode) ? null : natureCode.Trim();
        VatAmount = vatAmount;
        GrossAmount = grossAmount;
        CostCenter = Normalize(costCenter);
        Project = Normalize(project);
    }

    private PayableDocumentLine() { }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
