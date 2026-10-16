namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// Customer-role profile attached to a Contact via shared primary key (Party-Role pattern).
/// </summary>
public class CustomerProfile
{
    public Guid ContactId { get; set; }
    public Guid? PriceListId { get; set; }
    public Guid? PaymentTermId { get; set; }
    public decimal FinancialDiscountRate { get; set; }
    public decimal CreditLimit { get; set; }
    public bool IsBlocked { get; set; }
    public string? BlockReason { get; set; }
    public Guid? DefaultAgentId { get; set; }
    public string? DeliveryNotes { get; set; }

    // Navigation
    public Contact Contact { get; set; } = null!;
}
