using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Exceptions;
using Mauw.Erp.Domain.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Mauw.Erp.Application.Services;

/// <summary>
/// Gestisce l'anagrafica "Nostra Azienda" (IsInternalCompany == true) con cache in memoria.
/// </summary>
public class CompanySettingsService(
    IUnitOfWork unitOfWork,
    IMemoryCache cache,
    ILogger<CompanySettingsService> logger) : ICompanySettingsService
{
    private const string CacheKey = "internal_company";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public async Task<OperationResult<CompanySettingsDto>> GetCompanyAsync()
    {
        if (cache.TryGetValue(CacheKey, out CompanySettingsDto? cached))
            return OperationResult<CompanySettingsDto>.Ok(cached!);

        var contact = await unitOfWork.Contacts.GetInternalCompanyAsync();
        if (contact is null)
            return OperationResult<CompanySettingsDto>.Fail("Anagrafica aziendale non ancora configurata.");

        var dto = ContactMapper.MapToCompanySettings(contact);
        cache.Set(CacheKey, dto, CacheDuration);
        return OperationResult<CompanySettingsDto>.Ok(dto);
    }

    public async Task<OperationResult<CompanySettingsDto>> UpsertCompanyAsync(UpdateCompanySettingsRequest request)
    {
        var contact = await unitOfWork.Contacts.GetInternalCompanyAsync();

        var vatNumber = Normalize(request.VatNumber);
        var taxCode   = Normalize(request.TaxCode);

        if (contact is null)
        {
            contact = new Contact(request.BusinessName, vatNumber, taxCode)
            {
                IsInternalCompany = true,
                Status = ContactStatus.Active
            };
            ApplyCompanyFields(contact, request);
            contact.SetLegalHeadquarters(
                request.LegalHeadquarters.Street,
                request.LegalHeadquarters.ZipCode,
                request.LegalHeadquarters.City,
                request.LegalHeadquarters.Province,
                request.LegalHeadquarters.CountryCode,
                request.LegalHeadquarters.Phone,
                request.LegalHeadquarters.Email);
            await unitOfWork.Contacts.AddAsync(contact);
        }
        else
        {
            contact.BusinessName = request.BusinessName;
            contact.VatNumber    = vatNumber;
            contact.TaxCode      = taxCode;
            ApplyCompanyFields(contact, request);
            contact.SetLegalHeadquarters(
                request.LegalHeadquarters.Street,
                request.LegalHeadquarters.ZipCode,
                request.LegalHeadquarters.City,
                request.LegalHeadquarters.Province,
                request.LegalHeadquarters.CountryCode,
                request.LegalHeadquarters.Phone,
                request.LegalHeadquarters.Email);
            await unitOfWork.Contacts.UpdateAsync(contact);
        }

        await unitOfWork.SaveChangesAsync();
        cache.Remove(CacheKey);

        var saved = await unitOfWork.Contacts.GetInternalCompanyAsync();
        var dto = ContactMapper.MapToCompanySettings(saved!);
        cache.Set(CacheKey, dto, CacheDuration);
        logger.LogInformation("Company settings upserted for {BusinessName}", contact.BusinessName);
        return OperationResult<CompanySettingsDto>.Ok(dto);
    }

    public async Task<OperationResult<CompanyAddressSiteDto>> AddOperationalSiteAsync(CompanyAddressSiteRequest request)
    {
        var contact = await unitOfWork.Contacts.GetInternalCompanyAsync();
        if (contact is null) return OperationResult<CompanyAddressSiteDto>.Fail("Azienda non configurata.");

        if (!Enum.TryParse<AddressPurpose>(request.Purpose, true, out var purpose) ||
            purpose == AddressPurpose.LegalHeadquarters)
            purpose = AddressPurpose.OperationalSite;

        contact.AddOperationalSite(purpose,
            request.Street, request.ZipCode, request.City,
            request.Province, request.CountryCode,
            request.SiteName, request.IsMainOperationalSite,
            request.Phone, request.Email, request.AccessNotes);

        await unitOfWork.Contacts.UpdateAsync(contact);
        await unitOfWork.SaveChangesAsync();
        cache.Remove(CacheKey);

        var newSite = contact.Addresses
            .OrderByDescending(a => a.CreatedAt)
            .First(a => a.Purpose == purpose);

        return OperationResult<CompanyAddressSiteDto>.Ok(MapSite(newSite));
    }

    public async Task<OperationResult<CompanyAddressSiteDto>> UpdateOperationalSiteAsync(Guid siteId, CompanyAddressSiteRequest request)
    {
        var contact = await unitOfWork.Contacts.GetInternalCompanyAsync();
        if (contact is null) return OperationResult<CompanyAddressSiteDto>.Fail("Azienda non configurata.");

        var site = contact.Addresses.FirstOrDefault(a => a.Id == siteId);
        if (site is null) return OperationResult<CompanyAddressSiteDto>.Fail("Sede non trovata.");
        if (site.Purpose == AddressPurpose.LegalHeadquarters)
            return OperationResult<CompanyAddressSiteDto>.Fail("Usare l'endpoint PUT /settings/company per aggiornare la Sede Legale.");

        site.SiteName              = request.SiteName;
        site.Street                = request.Street;
        site.ZipCode               = request.ZipCode;
        site.City                  = request.City;
        site.Province              = request.Province;
        site.CountryCode           = request.CountryCode;
        site.Phone                 = request.Phone;
        site.Email                 = request.Email;
        site.AccessNotes           = request.AccessNotes;
        site.IsMainOperationalSite = request.IsMainOperationalSite;

        await unitOfWork.Contacts.UpdateAsync(contact);
        await unitOfWork.SaveChangesAsync();
        cache.Remove(CacheKey);

        return OperationResult<CompanyAddressSiteDto>.Ok(MapSite(site));
    }

    public async Task<OperationResult<bool>> DeleteOperationalSiteAsync(Guid siteId)
    {
        var contact = await unitOfWork.Contacts.GetInternalCompanyAsync();
        if (contact is null) return OperationResult<bool>.Fail("Azienda non configurata.");

        try { contact.RemoveAddress(siteId); }
        catch (DomainException ex) { return OperationResult<bool>.Fail(ex.Message); }

        await unitOfWork.Contacts.UpdateAsync(contact);
        await unitOfWork.SaveChangesAsync();
        cache.Remove(CacheKey);
        return OperationResult<bool>.Ok(true);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Converte stringa vuota in null per evitare violazioni UNIQUE sui campi nullable.</summary>
    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ApplyCompanyFields(Contact c, UpdateCompanySettingsRequest r)
    {
        c.SdiCode           = Normalize(r.SdiCode);
        c.PecEmail          = Normalize(r.PecEmail);
        c.CountryCode       = string.IsNullOrWhiteSpace(r.CountryCode) ? "IT" : r.CountryCode;
        c.FiscalRegime      = Normalize(r.FiscalRegime);
        c.ReaNumber         = r.ReaNumber;
        c.ReaOffice         = r.ReaOffice;
        c.ShareCapital      = r.ShareCapital;
        c.IsSoleShareholder = r.IsSoleShareholder;
        c.IsInLiquidation   = r.IsInLiquidation;
        c.LogoUrl           = r.LogoUrl;
    }

    private static CompanyAddressSiteDto MapSite(ContactAddress a) => new(
        Id: a.Id, Purpose: a.Purpose.ToString(), SiteName: a.SiteName,
        Street: a.Street, ZipCode: a.ZipCode, City: a.City, Province: a.Province,
        CountryCode: a.CountryCode, IsMainOperationalSite: a.IsMainOperationalSite,
        Phone: a.Phone, Email: a.Email, AccessNotes: a.AccessNotes,
        Latitude: a.Latitude, Longitude: a.Longitude, FormattedAddress: a.FormattedAddress,
        LinkedWarehouseId: a.LinkedWarehouseId);
}
