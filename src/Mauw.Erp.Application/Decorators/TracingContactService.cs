using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Decorators;

public sealed class TracingContactService(IContactService inner) : TracingDecoratorBase, IContactService
{
    public Task<OperationResult<ContactDto>> CreateContactAsync(CreateContactRequest request) =>
        TraceAsync("Contact.Create", () => inner.CreateContactAsync(request));

    public Task<OperationResult<ContactDto>> GetContactAsync(Guid id) =>
        TraceAsync("Contact.Get", () => inner.GetContactAsync(id),
            a => a?.SetTag("contact.id", id));

    public Task<OperationResult<PagedResult<ContactDto>>> GetContactsAsync(int page, int pageSize, string? search = null) =>
        TraceAsync("Contact.GetAll", () => inner.GetContactsAsync(page, pageSize, search),
            a => a?.SetTag("search", search));

    public Task<OperationResult<ContactDto>> UpdateContactAsync(Guid id, UpdateContactRequest request) =>
        TraceAsync("Contact.Update", () => inner.UpdateContactAsync(id, request),
            a => a?.SetTag("contact.id", id));

    public Task<OperationResult<bool>> DeleteContactAsync(Guid id) =>
        TraceAsync("Contact.Delete", () => inner.DeleteContactAsync(id),
            a => a?.SetTag("contact.id", id));
}
