namespace Mauw.Erp.Domain.Entities;

using Enums;

/// <summary>
/// Contact entity representing customers, suppliers, and agents.
/// </summary>
public class Contact : Entity
{
    public ContactType Type { get; set; }
    public string BusinessName { get; set; }
    public string VatNumber { get; set; }
    public string? SdiCode { get; set; }
    public decimal DefaultDiscountRate { get; set; }
    public Guid? AgentId { get; set; } // FK to Agent Contact
    public decimal DefaultCommissionRate { get; set; }

    // Navigation
    public Contact? Agent { get; set; }
    public ICollection<Order> Orders { get; set; } = [];
    public ICollection<AgentCommission> Commissions { get; set; } = [];

    public Contact(
        ContactType type,
        string businessName,
        string vatNumber,
        string? sdiCode = null,
        decimal defaultDiscountRate = 0,
        Guid? agentId = null,
        decimal defaultCommissionRate = 0
    ) : base()
    {
        Type = type;
        BusinessName = businessName;
        VatNumber = vatNumber;
        SdiCode = sdiCode;
        DefaultDiscountRate = defaultDiscountRate;
        AgentId = agentId;
        DefaultCommissionRate = defaultCommissionRate;
    }
}
