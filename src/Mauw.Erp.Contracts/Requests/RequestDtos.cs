using Mauw.Erp.Contracts.Enums;

namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Request for creating a new contact.
/// </summary>
public class CreateContactRequest
{
    public int Type { get; set; } // ContactTypeEnum
    public string BusinessName { get; set; } = "";
    public string VatNumber { get; set; } = "";
    public string? SdiCode { get; set; }
    public decimal DefaultDiscountRate { get; set; }
    public decimal DefaultCommissionRate { get; set; }
    public Guid? AgentId { get; set; }
}

/// <summary>
/// Request for updating an existing contact.
/// </summary>
public class UpdateContactRequest
{
    public string? BusinessName { get; set; }
    public string? VatNumber { get; set; }
    public string? SdiCode { get; set; }
    public decimal? DefaultDiscountRate { get; set; }
    public decimal? DefaultCommissionRate { get; set; }
    public Guid? AgentId { get; set; }
}

/// <summary>
/// Request for creating a new product.
/// </summary>
public class CreateProductRequest
{
    public Guid SupplierId { get; set; }
    public string SupplierCode { get; set; } = "";
    public string InternalCode { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal DefaultMarkupRate { get; set; }
    public decimal VatRate { get; set; }
    public string? ImageUrl { get; set; }
    public List<CreateProductVariantRequest> Variants { get; set; } = new();
}

/// <summary>
/// Request for creating a product variant.
/// </summary>
public class CreateProductVariantRequest
{
    public string Size { get; set; } = "";
    public string Color { get; set; } = "";
    public string Barcode { get; set; } = "";
    public decimal PurchaseCost { get; set; }
    public int StockQuantity { get; set; }
}

/// <summary>
/// Request for creating an order.
/// </summary>
public class CreateOrderRequest
{
    public Guid ContactId { get; set; }
    public decimal? DiscountRate { get; set; }
}

/// <summary>
/// Request for creating an order item.
/// </summary>
public class CreateOrderItemRequest
{
    public Guid VariantId { get; set; }
    public int Quantity { get; set; }
}

/// <summary>
/// Request for creating an invoice.
/// </summary>
public class CreateInvoiceRequest
{
    public Guid ContactId { get; set; }
}

/// <summary>
/// Request for creating an invoice item.
/// </summary>
public class CreateInvoiceItemRequest
{
    public Guid VariantId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitNetPrice { get; set; }
    public decimal UnitVatAmount { get; set; }
}

/// <summary>
/// Request for recording a payment.
/// </summary>
public class RecordPaymentRequest
{
    public decimal Amount { get; set; }
    public DateTime? PaymentDate { get; set; }
}

/// <summary>
/// Request for creating a DDT (transport document).
/// </summary>
public class CreateDdtRequest
{
    public Guid ContactId { get; set; }
    public string DdtNumber { get; set; } = "";
}

/// <summary>
/// Request for scanning inventory by barcode.
/// </summary>
public class BarcodeScanRequest
{
    public string Barcode { get; set; } = "";
    public int Quantity { get; set; }
    public int Type { get; set; } // StockMovementType enum
    public string? ReferenceDoc { get; set; }
}

/// <summary>
/// Request for importing SDI XML.
/// </summary>
public class SdiXmlImportRequest
{
    public string FilePath { get; set; } = "";
}
