using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Decorators;

public sealed class TracingDdtService(IDdtService inner) : TracingDecoratorBase, IDdtService
{
    public Task<OperationResult<DdtDto>> CreateDdtAsync(CreateDdtRequest request) =>
        TraceAsync("Ddt.Create", () => inner.CreateDdtAsync(request),
            a => a?.SetTag("contact.id", request.ContactId));

    public Task<OperationResult<DdtDto>> GetDdtAsync(Guid id) =>
        TraceAsync("Ddt.Get", () => inner.GetDdtAsync(id),
            a => a?.SetTag("ddt.id", id));

    public Task<OperationResult<PagedResult<DdtDto>>> GetDdtsByContactAsync(Guid contactId, int page, int pageSize) =>
        TraceAsync("Ddt.GetByContact", () => inner.GetDdtsByContactAsync(contactId, page, pageSize),
            a => a?.SetTag("contact.id", contactId));

    public Task<OperationResult<DdtDto>> AddItemAsync(Guid ddtId, CreateOrderItemRequest request) =>
        TraceAsync("Ddt.AddItem", () => inner.AddItemAsync(ddtId, request),
            a => a?.SetTag("ddt.id", ddtId));

    public Task<OperationResult<bool>> DeleteDdtAsync(Guid id) =>
        TraceAsync("Ddt.Delete", () => inner.DeleteDdtAsync(id),
            a => a?.SetTag("ddt.id", id));
}
