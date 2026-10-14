namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Request for updating an existing contact.
/// </summary>
public class UpdateContactRequest
{
    public string? BusinessName { get; set; }
    public string? VatNumber { get; set; }
    public string? SdiCode { get; set; }
    public decimal? DefaultDiscountRate { get; set; }
    public decimal? DefaultCommissionRate { get; set; }
    public Guid? AgentId { get; set; }
}