using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Application.Mappers;

/// <summary>
/// Manual mapper for Contact entities to DTOs without reflection.
/// </summary>
public static class ContactMapper
{
    public static ContactDto MapToDto(Contact contact)
    {
        return new ContactDto(
            Id: contact.Id,
            Type: (int)contact.Type,
            BusinessName: contact.BusinessName,
            VatNumber: contact.VatNumber,
            SdiCode: contact.SdiCode,
            DefaultDiscountRate: contact.DefaultDiscountRate,
            DefaultCommissionRate: contact.DefaultCommissionRate,
            AgentId: contact.AgentId,
            CreatedAt: contact.CreatedAt,
            UpdatedAt: contact.UpdatedAt
        );
    }

    public static List<ContactDto> MapToListDto(IEnumerable<Contact> contacts)
    {
        return contacts.Select(MapToDto).ToList();
    }
}