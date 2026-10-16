using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Application.Mappers;

/// <summary>
/// Manual mapper for Contact entities to DTOs without reflection.
/// </summary>
public static class ContactMapper
{
    public static ContactSummaryDto MapToSummary(Contact contact)
    {
        var legalAddress = contact.Addresses.FirstOrDefault(a => a.Purpose == Domain.Enums.AddressPurpose.LegalHeadquarters)
                        ?? contact.Addresses.FirstOrDefault();

        return new(
            Id: contact.Id,
            BusinessName: contact.BusinessName,
            VatNumber: contact.VatNumber,
            TaxCode: contact.TaxCode,
            City: legalAddress?.City,
            Province: legalAddress?.Province,
            Status: contact.Status.ToString(),
            IsCustomer: contact.IsCustomer,
            IsSupplier: contact.IsSupplier,
            IsAgent: contact.IsAgent,
            CreatedAt: contact.CreatedAt
        );
    }

    public static ContactDetailDto MapToDetail(Contact contact) => new(
        Id: contact.Id,
        BusinessName: contact.BusinessName,
        VatNumber: contact.VatNumber,
        TaxCode: contact.TaxCode,
        SdiCode: contact.SdiCode,
        PecEmail: contact.PecEmail,
        CountryCode: contact.CountryCode,
        FiscalRegime: contact.FiscalRegime,
        Status: contact.Status.ToString(),
        IsDeleted: contact.IsDeleted,
        CreatedAt: contact.CreatedAt,
        UpdatedAt: contact.UpdatedAt,
        CustomerProfile: contact.CustomerProfile is null ? null : MapCustomerProfile(contact.CustomerProfile),
        SupplierProfile: contact.SupplierProfile is null ? null : MapSupplierProfile(contact.SupplierProfile),
        AgentProfile: contact.AgentProfile is null ? null : MapAgentProfile(contact.AgentProfile),
        Addresses: contact.Addresses.Select(MapAddress).ToList(),
        Persons: contact.Persons.Select(MapPerson).ToList()
    );

    private static CustomerProfileDto MapCustomerProfile(CustomerProfile p) => new(
        PriceListId: p.PriceListId,
        PaymentTermId: p.PaymentTermId,
        FinancialDiscountRate: p.FinancialDiscountRate,
        CreditLimit: p.CreditLimit,
        IsBlocked: p.IsBlocked,
        BlockReason: p.BlockReason,
        DefaultAgentId: p.DefaultAgentId,
        DeliveryNotes: p.DeliveryNotes
    );

    private static SupplierProfileDto MapSupplierProfile(SupplierProfile p) => new(
        PaymentTermId: p.PaymentTermId,
        Iban: p.Iban,
        BicSwift: p.BicSwift
    );

    private static AgentProfileDto MapAgentProfile(AgentProfile p) => new(
        DefaultCommissionRate: p.DefaultCommissionRate,
        OperatingArea: p.OperatingArea
    );

    private static ContactAddressDto MapAddress(ContactAddress a) => new(
        Id: a.Id,
        Purpose: a.Purpose.ToString(),
        SiteName: a.SiteName,
        Street: a.Street,
        ZipCode: a.ZipCode,
        City: a.City,
        Province: a.Province,
        CountryCode: a.CountryCode,
        IsMainOperationalSite: a.IsMainOperationalSite,
        Phone: a.Phone,
        Email: a.Email,
        AccessNotes: a.AccessNotes,
        LinkedWarehouseId: a.LinkedWarehouseId
    );

    private static ContactPersonDto MapPerson(ContactPerson p) => new(
        Id: p.Id,
        FirstName: p.FirstName,
        LastName: p.LastName,
        Role: p.Role,
        Email: p.Email,
        Phone: p.Phone,
        IsPrimary: p.IsPrimary
    );

    public static List<ContactSummaryDto> MapToSummaryList(IEnumerable<Contact> contacts) =>
        contacts.Select(MapToSummary).ToList();

    public static CompanySettingsDto MapToCompanySettings(Contact c)
    {
        var hq = c.Addresses.FirstOrDefault(a => a.Purpose == Domain.Enums.AddressPurpose.LegalHeadquarters);
        var sites = c.Addresses
            .Where(a => a.Purpose != Domain.Enums.AddressPurpose.LegalHeadquarters)
            .Select(MapAddressToSite)
            .ToList();

        return new(
            Id: c.Id,
            BusinessName: c.BusinessName,
            VatNumber: c.VatNumber,
            TaxCode: c.TaxCode,
            SdiCode: c.SdiCode,
            PecEmail: c.PecEmail,
            CountryCode: c.CountryCode,
            FiscalRegime: c.FiscalRegime,
            ReaNumber: c.ReaNumber,
            ReaOffice: c.ReaOffice,
            ShareCapital: c.ShareCapital,
            IsSoleShareholder: c.IsSoleShareholder,
            IsInLiquidation: c.IsInLiquidation,
            LogoUrl: c.LogoUrl,
            LegalHeadquarters: hq is null ? null : MapAddressToSite(hq),
            OperationalSites: sites
        );
    }

    private static CompanyAddressSiteDto MapAddressToSite(ContactAddress a) => new(
        Id: a.Id,
        Purpose: a.Purpose.ToString(),
        SiteName: a.SiteName,
        Street: a.Street,
        ZipCode: a.ZipCode,
        City: a.City,
        Province: a.Province,
        CountryCode: a.CountryCode,
        IsMainOperationalSite: a.IsMainOperationalSite,
        Phone: a.Phone,
        Email: a.Email,
        AccessNotes: a.AccessNotes,
        LinkedWarehouseId: a.LinkedWarehouseId
    );
}