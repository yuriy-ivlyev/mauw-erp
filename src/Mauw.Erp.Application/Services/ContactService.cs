using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Exceptions;
using Mauw.Erp.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Mauw.Erp.Application.Services;

/// <summary>
/// Contact service implementing Party-Role pattern, soft delete and customer block management.
/// </summary>
public class ContactService(
    IUnitOfWork unitOfWork,
    IValidator<CreateContactRequest> createValidator,
    IValidator<UpdateContactRequest> updateValidator,
    ILogger<ContactService> logger,
    IPriceListRepository priceLists)
    : IContactService
{
    public async Task<OperationResult<PagedResult<ContactSummaryDto>>> GetPagedAsync(ContactFilterRequest request)
    {
        var (items, total) = await unitOfWork.Contacts.GetPagedAsync(
            request.Page,
            request.PageSize,
            request.Search,
            request.IsCustomer,
            request.IsSupplier,
            request.IsAgent,
            request.Status,
            request.SortBy,
            request.SortDescending);

        var dtos = ContactMapper.MapToSummaryList(items);
        return OperationResult<PagedResult<ContactSummaryDto>>.Ok(
            new PagedResult<ContactSummaryDto>(dtos, request.Page, request.PageSize, total));
    }

    public async Task<OperationResult<ContactDetailDto>> GetByIdAsync(Guid id)
    {
        var contact = await unitOfWork.Contacts.GetByIdWithDetailsAsync(id);
        if (contact is null)
            return OperationResult<ContactDetailDto>.Fail("Contact not found");

        return OperationResult<ContactDetailDto>.Ok(ContactMapper.MapToDetail(contact));
    }

    public async Task<OperationResult<ContactDetailDto>> CreateAsync(CreateContactRequest request)
    {
        var validation = createValidator.Validate(request);
        if (!validation.IsValid)
            return OperationResult<ContactDetailDto>.Fail(JoinErrors(validation));
        if (request.CustomerProfile?.PriceListId is Guid priceListId &&
            await priceLists.GetAsync(priceListId, CancellationToken.None) is null)
            return OperationResult<ContactDetailDto>.Fail("Listino cliente non trovato.");

        var contact = new Contact(request.BusinessName, Normalize(request.VatNumber), Normalize(request.TaxCode))
        {
            TradeName = Normalize(request.TradeName),
            SdiCode = Normalize(request.SdiCode),
            PecEmail = Normalize(request.PecEmail),
            CountryCode = string.IsNullOrWhiteSpace(request.CountryCode) ? "IT" : request.CountryCode,
            FiscalRegime = Normalize(request.FiscalRegime)
        };

        if (Enum.TryParse<ContactStatus>(request.Status, true, out var status))
            contact.Status = status;

        ApplyProfiles(contact, request.CustomerProfile, request.SupplierProfile, request.AgentProfile);

        foreach (var a in request.Addresses)
        {
            if (Enum.TryParse<AddressPurpose>(a.Purpose, true, out var purpose))
                contact.Addresses.Add(new ContactAddress(contact.Id, purpose,
                    a.Street, a.ZipCode, a.City, a.Province, a.CountryCode ?? "IT",
                    a.SiteName, a.IsMainOperationalSite, a.Phone, a.Email, a.AccessNotes));
        }

        foreach (var p in request.Persons)
            contact.Persons.Add(new ContactPerson(contact.Id, p.FirstName, p.LastName,
                p.Role, p.Email, p.Phone, p.IsPrimary));

        await unitOfWork.Contacts.AddAsync(contact);

        try
        {
            await unitOfWork.SaveChangesAsync();
            logger.LogInformation("Contact {Id} created: {BusinessName}", contact.Id, contact.BusinessName);
        }
        catch (ConcurrencyException ex)
        {
            return OperationResult<ContactDetailDto>.Fail(ex.Message);
        }

        var detail = await unitOfWork.Contacts.GetByIdWithDetailsAsync(contact.Id);
        return OperationResult<ContactDetailDto>.Ok(ContactMapper.MapToDetail(detail!));
    }

    public async Task<OperationResult<ContactDetailDto>> UpdateAsync(Guid id, UpdateContactRequest request)
    {
        var validation = updateValidator.Validate(request);
        if (!validation.IsValid)
            return OperationResult<ContactDetailDto>.Fail(JoinErrors(validation));
        if (!request.DisableCustomer && request.CustomerProfile?.PriceListId is Guid priceListId &&
            await priceLists.GetAsync(priceListId, CancellationToken.None) is null)
            return OperationResult<ContactDetailDto>.Fail("Listino cliente non trovato.");

        var contact = await unitOfWork.Contacts.GetByIdWithDetailsAsync(id);
        if (contact is null)
            return OperationResult<ContactDetailDto>.Fail("Contact not found");

        if (!string.IsNullOrWhiteSpace(request.BusinessName)) contact.BusinessName = request.BusinessName;
        if (request.TradeName is not null) contact.TradeName = Normalize(request.TradeName);
        if (request.VatNumber is not null) contact.VatNumber = Normalize(request.VatNumber);
        if (request.TaxCode   is not null) contact.TaxCode   = Normalize(request.TaxCode);
        if (request.SdiCode   is not null) contact.SdiCode   = Normalize(request.SdiCode);
        if (request.PecEmail  is not null) contact.PecEmail  = Normalize(request.PecEmail);
        if (!string.IsNullOrWhiteSpace(request.CountryCode)) contact.CountryCode = request.CountryCode;
        if (request.FiscalRegime is not null) contact.FiscalRegime = Normalize(request.FiscalRegime);

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<ContactStatus>(request.Status, true, out var status))
            contact.Status = status;

        // Roles
        if (request.DisableCustomer) contact.DisableAsCustomer();
        else if (request.CustomerProfile is not null)
        {
            contact.EnableAsCustomer();
            UpdateCustomerProfile(contact.CustomerProfile!, request.CustomerProfile);
        }

        if (request.DisableSupplier) contact.DisableAsSupplier();
        else if (request.SupplierProfile is not null)
        {
            contact.EnableAsSupplier();
            UpdateSupplierProfile(contact.SupplierProfile!, request.SupplierProfile);
        }

        if (request.DisableAgent) contact.DisableAsAgent();
        else if (request.AgentProfile is not null)
        {
            contact.EnableAsAgent();
            UpdateAgentProfile(contact.AgentProfile!, request.AgentProfile);
        }

        // Replace addresses if supplied
        if (request.Addresses is not null)
        {
            contact.Addresses.Clear();
            foreach (var a in request.Addresses)
            {
                if (Enum.TryParse<AddressPurpose>(a.Purpose, true, out var purpose))
                    contact.Addresses.Add(new ContactAddress(contact.Id, purpose,
                        a.Street, a.ZipCode, a.City, a.Province, a.CountryCode ?? "IT",
                        a.SiteName, a.IsMainOperationalSite, a.Phone, a.Email, a.AccessNotes));
            }
        }

        // Replace persons if supplied
        if (request.Persons is not null)
        {
            contact.Persons.Clear();
            foreach (var p in request.Persons)
                contact.Persons.Add(new ContactPerson(contact.Id, p.FirstName, p.LastName,
                    p.Role, p.Email, p.Phone, p.IsPrimary));
        }

        await unitOfWork.Contacts.UpdateAsync(contact);

        try
        {
            await unitOfWork.SaveChangesAsync();
            logger.LogInformation("Contact {Id} updated", id);
        }
        catch (ConcurrencyException ex)
        {
            return OperationResult<ContactDetailDto>.Fail(ex.Message);
        }

        var updated = await unitOfWork.Contacts.GetByIdWithDetailsAsync(id);
        return OperationResult<ContactDetailDto>.Ok(ContactMapper.MapToDetail(updated!));
    }

    public async Task<OperationResult<bool>> DeleteAsync(Guid id)
    {
        var contact = await unitOfWork.Contacts.GetByIdAsync(id);
        if (contact is null)
            return OperationResult<bool>.Fail("Contact not found");

        contact.SoftDelete();
        await unitOfWork.Contacts.UpdateAsync(contact);

        try
        {
            await unitOfWork.SaveChangesAsync();
            logger.LogInformation("Contact {Id} soft-deleted", id);
        }
        catch (ConcurrencyException ex)
        {
            return OperationResult<bool>.Fail(ex.Message);
        }

        return OperationResult<bool>.Ok(true);
    }

    public async Task<OperationResult<bool>> ToggleBlockCustomerAsync(Guid contactId, bool isBlocked, string? reason)
    {
        var contact = await unitOfWork.Contacts.GetByIdWithDetailsAsync(contactId);
        if (contact is null)
            return OperationResult<bool>.Fail("Contact not found");

        if (contact.CustomerProfile is null)
            return OperationResult<bool>.Fail("Contact does not have a customer profile");

        contact.CustomerProfile.IsBlocked = isBlocked;
        contact.CustomerProfile.BlockReason = isBlocked ? reason : null;

        await unitOfWork.Contacts.UpdateAsync(contact);

        try
        {
            await unitOfWork.SaveChangesAsync();
        }
        catch (ConcurrencyException ex)
        {
            return OperationResult<bool>.Fail(ex.Message);
        }

        return OperationResult<bool>.Ok(true);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void ApplyProfiles(
        Contact contact,
        CustomerProfileRequest? cp,
        SupplierProfileRequest? sp,
        AgentProfileRequest? ap)
    {
        if (cp is not null)
        {
            contact.EnableAsCustomer();
            UpdateCustomerProfile(contact.CustomerProfile!, cp);
        }

        if (sp is not null)
        {
            contact.EnableAsSupplier();
            UpdateSupplierProfile(contact.SupplierProfile!, sp);
        }

        if (ap is not null)
        {
            contact.EnableAsAgent();
            UpdateAgentProfile(contact.AgentProfile!, ap);
        }
    }

    private static void UpdateCustomerProfile(CustomerProfile profile, CustomerProfileRequest req)
    {
        profile.PriceListId = req.PriceListId;
        profile.PaymentTermId = req.PaymentTermId;
        profile.FinancialDiscountRate = req.FinancialDiscountRate;
        profile.CreditLimit = req.CreditLimit;
        profile.IsBlocked = req.IsBlocked;
        profile.BlockReason = req.BlockReason;
        profile.DefaultAgentId = req.DefaultAgentId;
        profile.DeliveryNotes = req.DeliveryNotes;
    }

    private static void UpdateSupplierProfile(SupplierProfile profile, SupplierProfileRequest req)
    {
        profile.PaymentTermId = req.PaymentTermId;
        profile.Iban = req.Iban;
        profile.BicSwift = req.BicSwift;
    }

    private static void UpdateAgentProfile(AgentProfile profile, AgentProfileRequest req)
    {
        profile.DefaultCommissionRate = req.DefaultCommissionRate;
        profile.OperatingArea = req.OperatingArea;
    }

    private static string JoinErrors(Validation.ValidationResult result) =>
        string.Join("; ", result.Errors.Select(e => $"{e.PropertyName}: {e.Message}"));

    /// <summary>Converte stringa vuota/whitespace in null, evitando violazioni UNIQUE su colonne nullable.</summary>
    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
