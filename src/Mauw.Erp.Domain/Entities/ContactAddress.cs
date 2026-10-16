namespace Mauw.Erp.Domain.Entities;

using Enums;

/// <summary>
/// Sede/indirizzo di un contatto. Supporta sede legale, siti operativi, magazzini.
/// Il campo LinkedWarehouseId è predisposto per il collegamento futuro ai magazzini.
/// </summary>
public class ContactAddress : Entity<Guid>
{
    public Guid ContactId { get; set; }
    public AddressPurpose Purpose { get; set; } = AddressPurpose.LegalHeadquarters;
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
    /// <summary>FK predisposto per collegamento futuro al modulo Magazzini.</summary>
    public Guid? LinkedWarehouseId { get; set; }

    // Navigation
    public Contact Contact { get; set; } = null!;

    private ContactAddress() : base() { } // for EF Core

    public ContactAddress(
        Guid contactId,
        AddressPurpose purpose,
        string street,
        string zipCode,
        string city,
        string? province = null,
        string countryCode = "IT",
        string? siteName = null,
        bool isMainOperationalSite = false,
        string? phone = null,
        string? email = null,
        string? accessNotes = null) : base()
    {
        ContactId = contactId;
        Purpose = purpose;
        SiteName = siteName;
        Street = street;
        ZipCode = zipCode;
        City = city;
        Province = province;
        CountryCode = countryCode;
        IsMainOperationalSite = isMainOperationalSite;
        Phone = phone;
        Email = email;
        AccessNotes = accessNotes;
    }
}
