namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// Supplier-role profile attached to a Contact via shared primary key (Party-Role pattern).
/// </summary>
public class SupplierProfile
{
    public Guid ContactId { get; set; }
    public Guid? PaymentTermId { get; set; }
    public string? Iban { get; set; }
    public string? BicSwift { get; set; }

    // Navigation
    public Contact Contact { get; set; } = null!;
}
