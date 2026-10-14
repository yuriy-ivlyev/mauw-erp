using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Exceptions;
using Mauw.Erp.Domain.Repositories;

namespace Mauw.Erp.Application.Services;

/// <summary>
/// Contact service implementation.
/// </summary>
public class ContactService(
    IUnitOfWork unitOfWork,
    IValidator<CreateContactRequest> createValidator,
    IValidator<UpdateContactRequest> updateValidator)
    : IContactService
{
    public async Task<OperationResult<ContactDto>> CreateContactAsync(CreateContactRequest request)
    {
        var validationResult = createValidator.Validate(request);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.Message}"));
            return OperationResult<ContactDto>.Fail(errors);
        }

        var contactType = (ContactType)request.Type;
        var contact = new Contact(
            type: contactType,
            businessName: request.BusinessName,
            vatNumber: request.VatNumber,
            sdiCode: request.SdiCode,
            defaultDiscountRate: request.DefaultDiscountRate,
            defaultCommissionRate: request.DefaultCommissionRate,
            agentId: request.AgentId
        );

        await unitOfWork.Contacts.AddAsync(contact);
        await unitOfWork.SaveChangesAsync();

        return OperationResult<ContactDto>.Ok(ContactMapper.MapToDto(contact));
    }

    public async Task<OperationResult<ContactDto>> GetContactAsync(Guid id)
    {
        var contact = await unitOfWork.Contacts.GetByIdAsync(id);
        if (contact == null)
            return OperationResult<ContactDto>.Fail("Contact not found");

        return OperationResult<ContactDto>.Ok(ContactMapper.MapToDto(contact));
    }

    public async Task<OperationResult<PagedResult<ContactDto>>> GetContactsAsync(int page, int pageSize, string? search = null)
    {
        var contacts = await unitOfWork.Contacts.GetPaginatedAsync(page, pageSize, search);
        var total = await unitOfWork.Contacts.GetTotalCountAsync(search);

        var dtos = ContactMapper.MapToListDto(contacts);
        var result = new PagedResult<ContactDto>(dtos, page, pageSize, total);

        return OperationResult<PagedResult<ContactDto>>.Ok(result);
    }

    public async Task<OperationResult<ContactDto>> UpdateContactAsync(Guid id, UpdateContactRequest request)
    {
        var validationResult = updateValidator.Validate(request);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.Message}"));
            return OperationResult<ContactDto>.Fail(errors);
        }

        var contact = await unitOfWork.Contacts.GetByIdAsync(id);
        if (contact == null)
            return OperationResult<ContactDto>.Fail("Contact not found");

        if (!string.IsNullOrWhiteSpace(request.BusinessName))
            contact.BusinessName = request.BusinessName;

        if (!string.IsNullOrWhiteSpace(request.VatNumber))
            contact.VatNumber = request.VatNumber;

        if (!string.IsNullOrWhiteSpace(request.SdiCode))
            contact.SdiCode = request.SdiCode;

        if (request.DefaultDiscountRate.HasValue)
            contact.DefaultDiscountRate = request.DefaultDiscountRate.Value;

        if (request.DefaultCommissionRate.HasValue)
            contact.DefaultCommissionRate = request.DefaultCommissionRate.Value;

        if (request.AgentId.HasValue)
            contact.AgentId = request.AgentId.Value;

        await unitOfWork.Contacts.UpdateAsync(contact);
        try
        {
            await unitOfWork.SaveChangesAsync();
        }
        catch (ConcurrencyException ex)
        {
            return OperationResult<ContactDto>.Fail(ex.Message);
        }

        return OperationResult<ContactDto>.Ok(ContactMapper.MapToDto(contact));
    }

    public async Task<OperationResult<bool>> DeleteContactAsync(Guid id)
    {
        var contact = await unitOfWork.Contacts.GetByIdAsync(id);
        if (contact == null)
            return OperationResult<bool>.Fail("Contact not found");

        await unitOfWork.Contacts.DeleteAsync(contact);
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
}