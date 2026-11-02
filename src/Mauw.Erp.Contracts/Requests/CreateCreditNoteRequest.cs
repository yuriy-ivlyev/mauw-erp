namespace Mauw.Erp.Contracts.Requests;

public sealed record CreateCreditNoteRequest(
    Guid InvoiceId,
    string Reason,
    IReadOnlyList<CreateCreditNoteLineRequest> Lines,
    bool ReturnToWarehouse = false,
    Guid? WarehouseId = null);

public sealed record CreateCreditNoteLineRequest(
    Guid? InvoiceItemId,
    bool IsShippingCharge,
    decimal Quantity);
