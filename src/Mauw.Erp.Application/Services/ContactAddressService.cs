using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Exceptions;
using Mauw.Erp.Domain.Repositories;

namespace Mauw.Erp.Application.Services;

/// <summary>
/// Gestione CRUD indirizzi/sedi dei contatti generici (non la Nostra Azienda).
/// Impedisce la cancellazione della Sede Legale.
/// </summary>
public class ContactAddressService(IUnitOfWork unitOfWork) : IContactAddressService
{
    public async Task<OperationResult<List<ContactAddressDto>>> GetAddressesAsync(Guid contactId)
    {
        var contact = await unitOfWork.Contacts.GetByIdWithDetailsAsync(contactId);
        if (contact is null) return OperationResult<List<ContactAddressDto>>.Fail("Contact not found");

        var dtos = contact.Addresses
            .Select(a => MapAddress(a))
            .ToList();
        return OperationResult<List<ContactAddressDto>>.Ok(dtos);
    }

    public async Task<OperationResult<ContactAddressDto>> AddAddressAsync(Guid contactId, ContactAddressRequest request)
    {
        var contact = await unitOfWork.Contacts.GetByIdWithDetailsAsync(contactId);
        if (contact is null) return OperationResult<ContactAddressDto>.Fail("Contact not found");

        if (!Enum.TryParse<AddressPurpose>(request.Purpose, true, out var purpose))
            purpose = AddressPurpose.OperationalSite;

        ContactAddress address;
        try
        {
            if (purpose == AddressPurpose.LegalHeadquarters)
                address = contact.SetLegalHeadquarters(request.Street, request.ZipCode, request.City,
                    request.Province, request.CountryCode ?? "IT", request.Phone, request.Email);
            else
                address = contact.AddOperationalSite(purpose,
                    request.Street, request.ZipCode, request.City,
                    request.Province, request.CountryCode ?? "IT",
                    request.SiteName, request.IsMainOperationalSite,
                    request.Phone, request.Email, request.AccessNotes);
        }
        catch (DomainException ex) { return OperationResult<ContactAddressDto>.Fail(ex.Message); }

        await unitOfWork.Contacts.UpdateAsync(contact);
        await unitOfWork.SaveChangesAsync();
        return OperationResult<ContactAddressDto>.Ok(MapAddress(address));
    }

    public async Task<OperationResult<ContactAddressDto>> UpdateAddressAsync(
        Guid contactId, Guid addressId, ContactAddressRequest request)
    {
        var contact = await unitOfWork.Contacts.GetByIdWithDetailsAsync(contactId);
        if (contact is null) return OperationResult<ContactAddressDto>.Fail("Contact not found");

        var addr = contact.Addresses.FirstOrDefault(a => a.Id == addressId);
        if (addr is null) return OperationResult<ContactAddressDto>.Fail("Address not found");

        addr.SiteName              = request.SiteName;
        addr.Street                = request.Street;
        addr.ZipCode               = request.ZipCode;
        addr.City                  = request.City;
        addr.Province              = request.Province;
        addr.CountryCode           = request.CountryCode ?? "IT";
        addr.IsMainOperationalSite = request.IsMainOperationalSite;
        addr.Phone                 = request.Phone;
        addr.Email                 = request.Email;
        addr.AccessNotes           = request.AccessNotes;

        await unitOfWork.Contacts.UpdateAsync(contact);
        await unitOfWork.SaveChangesAsync();
        return OperationResult<ContactAddressDto>.Ok(MapAddress(addr));
    }

    public async Task<OperationResult<bool>> DeleteAddressAsync(Guid contactId, Guid addressId)
    {
        var contact = await unitOfWork.Contacts.GetByIdWithDetailsAsync(contactId);
        if (contact is null) return OperationResult<bool>.Fail("Contact not found");

        try { contact.RemoveAddress(addressId); }
        catch (DomainException ex) { return OperationResult<bool>.Fail(ex.Message); }

        await unitOfWork.Contacts.UpdateAsync(contact);
        await unitOfWork.SaveChangesAsync();
        return OperationResult<bool>.Ok(true);
    }

    private static ContactAddressDto MapAddress(ContactAddress a) => new(
        Id: a.Id, Purpose: a.Purpose.ToString(), SiteName: a.SiteName,
        Street: a.Street, ZipCode: a.ZipCode, City: a.City, Province: a.Province,
        CountryCode: a.CountryCode, IsMainOperationalSite: a.IsMainOperationalSite,
        Phone: a.Phone, Email: a.Email, AccessNotes: a.AccessNotes,
        LinkedWarehouseId: a.LinkedWarehouseId);
}
