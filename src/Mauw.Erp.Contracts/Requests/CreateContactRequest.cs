namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Command for creating a new contact with optional role profiles.
/// </summary>
public class CreateContactRequest
{
    // Anagrafica
    public string BusinessName { get; set; } = "";
    public string? TradeName { get; set; }
    public string? VatNumber { get; set; }
    public string? TaxCode { get; set; }
    public string? SdiCode { get; set; }
    public string? PecEmail { get; set; }
    public string CountryCode { get; set; } = "IT";
    public string? FiscalRegime { get; set; }
    public string Status { get; set; } = "Draft";

    // Ruoli (null = ruolo non abilitato)
    public CustomerProfileRequest? CustomerProfile { get; set; }
    public SupplierProfileRequest? SupplierProfile { get; set; }
    public AgentProfileRequest? AgentProfile { get; set; }

    // Indirizzi e referenti
    public List<ContactAddressRequest> Addresses { get; set; } = [];
    public List<ContactPersonRequest> Persons { get; set; } = [];
}

public class CustomerProfileRequest
{
    public Guid? PriceListId { get; set; }
    public Guid? PaymentTermId { get; set; }
    public decimal FinancialDiscountRate { get; set; }
    public decimal CreditLimit { get; set; }
    public bool IsBlocked { get; set; }
    public string? BlockReason { get; set; }
    public Guid? DefaultAgentId { get; set; }
    public string? DeliveryNotes { get; set; }
}

public class SupplierProfileRequest
{
    public Guid? PaymentTermId { get; set; }
    public string? Iban { get; set; }
    public string? BicSwift { get; set; }
}

public class AgentProfileRequest
{
    public decimal DefaultCommissionRate { get; set; }
    public string? OperatingArea { get; set; }
}

public class ContactAddressRequest
{
    public string Purpose { get; set; } = "LegalHeadquarters";
    public string? SiteName { get; set; }
    public string Street { get; set; } = "";
    public string ZipCode { get; set; } = "";
    public string City { get; set; } = "";
    public string? Province { get; set; }
    public string CountryCode { get; set; } = "IT";
    public bool IsMainOperationalSite { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? AccessNotes { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? FormattedAddress { get; set; }
    public string? ExternalPlaceId { get; set; }
}

public class ContactPersonRequest
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Role { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsPrimary { get; set; }
}