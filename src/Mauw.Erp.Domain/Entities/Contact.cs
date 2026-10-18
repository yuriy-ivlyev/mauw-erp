namespace Mauw.Erp.Domain.Entities;

using Enums;
using Exceptions;

/// <summary>
/// Contact (Party) entity: any business counterpart (customer, supplier, agent or any combination).
/// Roles are implemented via optional 1-to-1 profile entities (Party-Role pattern).
/// IsInternalCompany = true identifica la nostra azienda (Company Settings).
/// Supports soft delete and Italian e-invoicing fields.
/// </summary>
public class Contact : Entity<Guid>
{
    // ── Anagrafica fiscale ────────────────────────────────────────────────────
    public string BusinessName { get; set; } = "";
    public string? VatNumber { get; set; }
    public string? TaxCode { get; set; }
    /// <summary>Codice Destinatario SDI (max 7 chars).</summary>
    public string? SdiCode { get; set; }
    public string? PecEmail { get; set; }
    public string CountryCode { get; set; } = "IT";
    /// <summary>Regime fiscale SDI (e.g. RF01, RF19).</summary>
    public string? FiscalRegime { get; set; }

    // ── Dati societari (Nostra Azienda / Internal Company) ───────────────────
    /// <summary>True per il record "Nostra Azienda" (Company Settings).</summary>
    public bool IsInternalCompany { get; set; }
    /// <summary>Numero REA (es. MI-1234567).</summary>
    public string? ReaNumber { get; set; }
    /// <summary>Ufficio REA (sigla provincia, es. MI).</summary>
    public string? ReaOffice { get; set; }
    public decimal? ShareCapital { get; set; }
    public bool IsShareCapitalFullyPaid { get; set; }
    public string? BankName { get; set; }
    public string? Iban { get; set; }
    public string? BicSwift { get; set; }
    public string? InvoiceNotes { get; set; }
    public bool? IsSoleShareholder { get; set; }
    public bool IsInLiquidation { get; set; }
    public string? LogoUrl { get; set; }
    public byte[]? HorizontalLogoPng { get; set; }
    public byte[]? CompactLogoPng { get; set; }

    // ── Stato e soft delete ───────────────────────────────────────────────────
    public ContactStatus Status { get; set; } = ContactStatus.Draft;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    // ── Profili di ruolo (Party-Role) ─────────────────────────────────────────
    public CustomerProfile? CustomerProfile { get; set; }
    public SupplierProfile? SupplierProfile { get; set; }
    public AgentProfile? AgentProfile { get; set; }

    // ── Collezioni ────────────────────────────────────────────────────────────
    public ICollection<ContactAddress> Addresses { get; set; } = [];
    public ICollection<ContactPerson> Persons { get; set; } = [];

    // ── Navigazioni esistenti (backward compat) ───────────────────────────────
    public ICollection<Order> Orders { get; set; } = [];
    public ICollection<AgentCommission> Commissions { get; set; } = [];

    // ── Computed ──────────────────────────────────────────────────────────────
    public bool IsCustomer => CustomerProfile is not null;
    public bool IsSupplier => SupplierProfile is not null;
    public bool IsAgent => AgentProfile is not null;
    public ContactAddress? LegalHeadquarters =>
        Addresses.FirstOrDefault(a => a.Purpose == AddressPurpose.LegalHeadquarters);

    // ── Costruttori ───────────────────────────────────────────────────────────
    private Contact() : base() { } // for EF Core

    public Contact(string businessName, string? vatNumber = null, string? taxCode = null) : base()
    {
        BusinessName = businessName;
        VatNumber = vatNumber;
        TaxCode = taxCode;
    }

    // ── Metodi di dominio: ruoli ──────────────────────────────────────────────

    public void EnableAsCustomer()
    {
        CustomerProfile ??= new CustomerProfile { ContactId = Id };
        UpdateTimestamp();
    }

    public void DisableAsCustomer()
    {
        CustomerProfile = null;
        UpdateTimestamp();
    }

    public void EnableAsSupplier()
    {
        SupplierProfile ??= new SupplierProfile { ContactId = Id };
        UpdateTimestamp();
    }

    public void DisableAsSupplier()
    {
        SupplierProfile = null;
        UpdateTimestamp();
    }

    public void EnableAsAgent()
    {
        AgentProfile ??= new AgentProfile { ContactId = Id };
        UpdateTimestamp();
    }

    public void DisableAsAgent()
    {
        AgentProfile = null;
        UpdateTimestamp();
    }

    // ── Metodi di dominio: stato ──────────────────────────────────────────────

    public void Activate()   { Status = ContactStatus.Active;    UpdateTimestamp(); }
    public void Suspend()    { Status = ContactStatus.Suspended; UpdateTimestamp(); }
    public void Archive()    { Status = ContactStatus.Archived;  UpdateTimestamp(); }

    public void SoftDelete(string? deletedBy = null)
    {
        IsDeleted      = true;
        DeletedAtUtc   = DateTime.UtcNow;
        DeletedBy      = deletedBy;
        UpdateTimestamp();
    }

    // ── Metodi di dominio: indirizzi ──────────────────────────────────────────

    /// <summary>
    /// Imposta (o sostituisce) la Sede Legale. Ne deve esistere sempre una sola.
    /// </summary>
    public ContactAddress SetLegalHeadquarters(
        string street, string zipCode, string city,
        string? province = null, string countryCode = "IT",
        string? phone = null, string? email = null)
    {
        var existing = LegalHeadquarters;
        if (existing is not null)
        {
            existing.Street = street;
            existing.ZipCode = zipCode;
            existing.City = city;
            existing.Province = province;
            existing.CountryCode = countryCode;
            existing.Phone = phone;
            existing.Email = email;
            UpdateTimestamp();
            return existing;
        }

        var hq = new ContactAddress(Id, AddressPurpose.LegalHeadquarters,
            street, zipCode, city, province, countryCode,
            siteName: null, isMainOperationalSite: false, phone, email);
        Addresses.Add(hq);
        UpdateTimestamp();
        return hq;
    }

    /// <summary>Aggiunge un sito operativo/magazzino/amministrativo.</summary>
    public ContactAddress AddOperationalSite(
        AddressPurpose purpose,
        string street, string zipCode, string city,
        string? province = null, string countryCode = "IT",
        string? siteName = null, bool isMain = false,
        string? phone = null, string? email = null,
        string? accessNotes = null)
    {
        if (purpose == AddressPurpose.LegalHeadquarters)
            throw new DomainException("Usare SetLegalHeadquarters per la Sede Legale.");

        var site = new ContactAddress(Id, purpose, street, zipCode, city,
            province, countryCode, siteName, isMain, phone, email, accessNotes);

        if (isMain)
            foreach (var s in Addresses.Where(a => a.Purpose == purpose))
                s.IsMainOperationalSite = false;

        Addresses.Add(site);
        UpdateTimestamp();
        return site;
    }

    /// <summary>Rimuove un indirizzo. Blocca la cancellazione della Sede Legale.</summary>
    public void RemoveAddress(Guid addressId)
    {
        var addr = Addresses.FirstOrDefault(a => a.Id == addressId)
            ?? throw new DomainException("Indirizzo non trovato.");

        if (addr.Purpose == AddressPurpose.LegalHeadquarters)
            throw new DomainException("La Sede Legale non può essere eliminata.");

        Addresses.Remove(addr);
        UpdateTimestamp();
    }
}
