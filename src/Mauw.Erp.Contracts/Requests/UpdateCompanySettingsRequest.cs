namespace Mauw.Erp.Contracts.Requests;

public class UpdateCompanySettingsRequest
{
    public string BusinessName { get; set; } = "";
    public string? VatNumber { get; set; }
    public string? TaxCode { get; set; }
    public string? SdiCode { get; set; }
    public string? PecEmail { get; set; }
    public string CountryCode { get; set; } = "IT";
    public string? FiscalRegime { get; set; }
    public string? ReaNumber { get; set; }
    public string? ReaOffice { get; set; }
    public decimal? ShareCapital { get; set; }
    public bool? IsSoleShareholder { get; set; }
    public bool IsInLiquidation { get; set; }
    public string? LogoUrl { get; set; }

    /// <summary>Sede Legale obbligatoria nell'upsert aziendale.</summary>
    public CompanyAddressSiteRequest LegalHeadquarters { get; set; } = new();
}

public class CompanyAddressSiteRequest
{
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
    /// <summary>Scopo opzionale per sedi operative (default OperationalSite).</summary>
    public string Purpose { get; set; } = "OperationalSite";
}
