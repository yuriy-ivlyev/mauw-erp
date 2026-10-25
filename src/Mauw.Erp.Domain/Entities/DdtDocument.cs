using Mauw.Erp.Domain.Accounting;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// DdtDocument entity representing a transport document (Documento di Trasporto).
/// Created from confirmed orders and decrements physical stock.
/// </summary>
public class DdtDocument : Entity<Guid>
{
    public Guid ContactId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? OrderId { get; set; }
    public string DdtNumber { get; set; }
    public DateTime DdtDate { get; set; }
    public decimal TaxableTotal { get; set; }
    public decimal VatTotal { get; set; }
    public decimal GrossTotal { get; set; }

    // Navigation
    public Contact Contact { get; set; } = null!;
    public Warehouse? Warehouse { get; set; }
    public ICollection<DdtItem> Items { get; set; } = [];

    public DdtDocument(
        Guid contactId,
        string ddtNumber,
        Guid? warehouseId = null
    ) : base()
    {
        ContactId = contactId;
        WarehouseId = warehouseId;
        DdtNumber = ddtNumber;
        DdtDate = DateTime.UtcNow;
        TaxableTotal = 0;
        VatTotal = 0;
        GrossTotal = 0;
    }

    /// <summary>
    /// Adds an item to the DDT and updates totals.
    /// </summary>
    public AccountingResult<CalculatedDocument> AddItem(DdtItem item, ItalianAccountingCalculator calculator)
    {
        var result = calculator.CalculateAndApply(Items.Append(item));
        if (!result.Success)
            return result;
        Items.Add(item);
        ApplyTotals(result.Data!);
        return result;
    }

    /// <summary>
    /// Recalculates DDT totals from items.
    /// </summary>
    public AccountingResult<CalculatedDocument> UpdateTotals(ItalianAccountingCalculator calculator)
    {
        var result = calculator.CalculateAndApply(Items);
        if (result.Success)
            ApplyTotals(result.Data!);
        return result;
    }

    private void ApplyTotals(CalculatedDocument document)
    {
        TaxableTotal = document.TotalTaxable;
        VatTotal = document.TotalVat;
        GrossTotal = document.TotalGross;
        UpdateTimestamp();
    }
}
