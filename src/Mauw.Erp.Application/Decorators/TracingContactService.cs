using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Decorators;

public sealed class TracingContactService(IContactService inner) : TracingDecoratorBase, IContactService
{
    public Task<OperationResult<PagedResult<ContactSummaryDto>>> GetPagedAsync(ContactFilterRequest request) =>
        TraceAsync("Contact.GetPaged", () => inner.GetPagedAsync(request),
            a => a?.SetTag("search", request.Search));

    public Task<OperationResult<ContactDetailDto>> GetByIdAsync(Guid id) =>
        TraceAsync("Contact.GetById", () => inner.GetByIdAsync(id),
            a => a?.SetTag("contact.id", id));

    public Task<OperationResult<ContactDetailDto>> CreateAsync(CreateContactRequest request) =>
        TraceAsync("Contact.Create", () => inner.CreateAsync(request));

    public Task<OperationResult<ContactDetailDto>> UpdateAsync(Guid id, UpdateContactRequest request) =>
        TraceAsync("Contact.Update", () => inner.UpdateAsync(id, request),
            a => a?.SetTag("contact.id", id));

    public Task<OperationResult<bool>> DeleteAsync(Guid id) =>
        TraceAsync("Contact.Delete", () => inner.DeleteAsync(id),
            a => a?.SetTag("contact.id", id));

    public Task<OperationResult<bool>> ToggleBlockCustomerAsync(Guid contactId, bool isBlocked, string? reason) =>
        TraceAsync("Contact.ToggleBlock", () => inner.ToggleBlockCustomerAsync(contactId, isBlocked, reason),
            a => a?.SetTag("contact.id", contactId).SetTag("blocked", isBlocked));
}
