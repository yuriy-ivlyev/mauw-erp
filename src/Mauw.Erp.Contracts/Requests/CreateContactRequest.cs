namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Request for creating a new contact.
/// </summary>
public class CreateContactRequest
{
    public int Type { get; set; } // ContactTypeEnum
    public string BusinessName { get; set; } = "";
    public string VatNumber { get; set; } = "";
    public string? SdiCode { get; set; }
    public decimal DefaultDiscountRate { get; set; }
    public decimal DefaultCommissionRate { get; set; }
    public Guid? AgentId { get; set; }
}