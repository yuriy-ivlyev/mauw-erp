using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;

namespace Mauw.Erp.Application.Services;

public interface ISupplierPurchaseOrderService
{
    Task<IReadOnlyList<SupplierPurchaseOrderDto>> ListAsync(Guid? supplierId, bool openOnly, CancellationToken ct);
    Task<SupplierPurchaseOrderDto?> GetAsync(Guid id, CancellationToken ct);
    Task<OperationResult<SupplierPurchaseOrderDto>> CreateAsync(CreateSupplierPurchaseOrderRequest request, CancellationToken ct);
    Task<OperationResult<SupplierPurchaseOrderDto>> ConfirmAsync(Guid id, Guid version, CancellationToken ct);
    Task<SupplierReconciliationDto?> ReconcileAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<SupplierShortageLineDto>?> ShortagesAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<SupplierReturnDraftDto>?> ReturnDraftsAsync(Guid id, CancellationToken ct);
    Task<SupplierReturnDraftDto?> GetReturnDraftAsync(Guid id, Guid draftId, CancellationToken ct);
    Task<OperationResult<SupplierReturnDraftDto>> CreateReturnDraftAsync(Guid id, CreateSupplierReturnDraftRequest request, CancellationToken ct);
    Task<OperationResult<SupplierReturnDraftDto>> IssueReturnDraftAsync(
        Guid id, Guid draftId, Guid version, CancellationToken ct);
    Task<OperationResult<SupplierPurchaseOrderDto>> CloseAsync(Guid id, Guid version, CancellationToken ct);
}
