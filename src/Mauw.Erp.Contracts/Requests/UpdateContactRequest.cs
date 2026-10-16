namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Command for full update of an existing contact (all fields optional, null = no change).
/// </summary>
public class UpdateContactRequest
{
    public string? BusinessName { get; set; }
    public string? VatNumber { get; set; }
    public string? TaxCode { get; set; }
    public string? SdiCode { get; set; }
    public string? PecEmail { get; set; }
    public string? CountryCode { get; set; }
    public string? FiscalRegime { get; set; }
    public string? Status { get; set; }

    public CustomerProfileRequest? CustomerProfile { get; set; }
    public bool DisableCustomer { get; set; }

    public SupplierProfileRequest? SupplierProfile { get; set; }
    public bool DisableSupplier { get; set; }

    public AgentProfileRequest? AgentProfile { get; set; }
    public bool DisableAgent { get; set; }

    public List<ContactAddressRequest>? Addresses { get; set; }
    public List<ContactPersonRequest>? Persons { get; set; }
}