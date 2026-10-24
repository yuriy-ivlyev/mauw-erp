namespace Mauw.Erp.Domain.Enums;

public enum SupplierDeliveryNoteStatus
{
    Draft,
    Parsing,
    Parsed,
    Confirmed,
    Cancelled
}

public enum SupplierDeliveryNoteLineStatus
{
    Matched,
    NewArticleNeeded,
    Error
}
