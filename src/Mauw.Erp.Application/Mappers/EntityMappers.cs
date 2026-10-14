using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.Application.Mappers;

/// <summary>
/// Manual mapper for Product entities to DTOs without reflection.
/// </summary>
public static class ProductMapper
{
    private static readonly PricingEngine PricingEngine = new();

    public static ProductDto MapToDto(Product product)
    {
        return new ProductDto(
            Id: product.Id,
            SupplierId: product.SupplierId,
            SupplierCode: product.SupplierCode,
            InternalCode: product.InternalCode,
            Name: product.Name,
            DefaultMarkupRate: product.DefaultMarkupRate,
            VatRate: product.VatRate,
            ImageUrl: product.ImageUrl,
            Variants: product.Variants.Select(v => MapVariantToDto(v, product.DefaultMarkupRate)).ToList()
        );
    }

    public static ProductVariantDto MapVariantToDto(ProductVariant variant, decimal defaultMarkupRate)
    {
        var pricingResult = PricingEngine.CalculatePrice(
            variant.PurchaseCost,
            defaultMarkupRate,
            0,
            0
        );

        return new ProductVariantDto(
            Id: variant.Id,
            ProductId: variant.ProductId,
            Size: variant.Size,
            Color: variant.Color,
            Barcode: variant.Barcode,
            PurchaseCost: variant.PurchaseCost,
            StockQuantity: variant.StockQuantity,
            ListPrice: pricingResult.ListPrice,
            FinalNetPrice: pricingResult.FinalNetPrice,
            VatAmount: 0,
            FinalGrossPrice: pricingResult.FinalNetPrice
        );
    }

    public static BarcodeVariantDto MapBarcodeVariantToDto(ProductVariant variant, Product product)
    {
        var pricingResult = PricingEngine.CalculatePrice(
            variant.PurchaseCost,
            product.DefaultMarkupRate,
            0,
            product.VatRate
        );

        return new BarcodeVariantDto(
            Barcode: variant.Barcode,
            VariantId: variant.Id,
            ProductId: variant.ProductId,
            ProductName: product.Name,
            Size: variant.Size,
            Color: variant.Color,
            StockQuantity: variant.StockQuantity,
            UnitNetPrice: pricingResult.FinalNetPrice,
            UnitGrossPrice: pricingResult.FinalGrossPrice,
            VatRate: product.VatRate
        );
    }

    public static List<ProductDto> MapToListDto(IEnumerable<Product> products)
    {
        return products.Select(MapToDto).ToList();
    }
}

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

/// <summary>
/// Manual mapper for Order entities to DTOs without reflection.
/// </summary>
public static class OrderMapper
{
    public static OrderDto MapToDto(Order order)
    {
        return new OrderDto(
            Id: order.Id,
            OrderNumber: order.OrderNumber,
            ContactId: order.ContactId,
            OrderDate: order.OrderDate,
            CustomerDiscountRate: order.CustomerDiscountRate,
            Status: order.Status.ToString(),
            TaxableTotal: order.TaxableTotal,
            VatTotal: order.VatTotal,
            GrossTotal: order.GrossTotal,
            Items: order.Items.Select(MapItemToDto).ToList()
        );
    }

    private static OrderItemDto MapItemToDto(OrderItem item)
    {
        return new OrderItemDto(
            Id: item.Id,
            OrderId: item.OrderId,
            VariantId: item.VariantId,
            Quantity: item.Quantity,
            UnitNetPrice: item.UnitNetPrice,
            VatRate: item.VatRate,
            LineNetTotal: item.LineNetTotal,
            LineVatTotal: item.LineVatTotal,
            LineGrossTotal: item.LineGrossTotal
        );
    }

    public static List<OrderDto> MapToListDto(IEnumerable<Order> orders)
    {
        return orders.Select(MapToDto).ToList();
    }
}

/// <summary>
/// Manual mapper for Invoice entities to DTOs without reflection.
/// </summary>
public static class InvoiceMapper
{
    public static InvoiceDto MapToDto(Invoice invoice)
    {
        return new InvoiceDto(
            Id: invoice.Id,
            InvoiceNumber: invoice.InvoiceNumber,
            ContactId: invoice.ContactId,
            InvoiceDate: invoice.InvoiceDate,
            TaxableTotal: invoice.TaxableTotal,
            VatTotal: invoice.VatTotal,
            GrossTotal: invoice.GrossTotal,
            Status: "Draft", // Default; can be enhanced with InvoiceStatus enum
            Items: invoice.Items.Select(MapItemToDto).ToList(),
            Payments: invoice.Payments.Select(MapPaymentToDto).ToList()
        );
    }

    private static InvoiceItemDto MapItemToDto(InvoiceItem item)
    {
        return new InvoiceItemDto(
            Id: item.Id,
            InvoiceId: item.InvoiceId,
            VariantId: item.VariantId,
            Quantity: item.Quantity,
            UnitNetPrice: item.UnitNetPrice,
            VatRate: item.VatRate,
            LineNetTotal: item.LineNetTotal,
            LineVatTotal: item.LineVatTotal,
            LineGrossTotal: item.LineGrossTotal
        );
    }

    private static PaymentDto MapPaymentToDto(Payment payment)
    {
        return new PaymentDto(
            Id: payment.Id,
            Amount: payment.Amount,
            PaymentDate: payment.PaymentDate,
            PaymentMethod: "Transfer" // Default; can be enhanced with PaymentMethod enum
        );
    }

    public static List<InvoiceDto> MapToListDto(IEnumerable<Invoice> invoices)
    {
        return invoices.Select(MapToDto).ToList();
    }
}

/// <summary>
/// Manual mapper for DdtDocument entities to DTOs without reflection.
/// </summary>
public static class DdtMapper
{
    public static DdtDto MapToDto(DdtDocument ddt)
    {
        return new DdtDto(
            Id: ddt.Id,
            DdtNumber: ddt.DdtNumber,
            ContactId: ddt.ContactId,
            DdtDate: ddt.DdtDate,
            TaxableTotal: ddt.TaxableTotal,
            VatTotal: ddt.VatTotal,
            GrossTotal: ddt.GrossTotal,
            Items: ddt.Items.Select(MapItemToDto).ToList()
        );
    }

    private static DdtItemDto MapItemToDto(DdtItem item)
    {
        return new DdtItemDto(
            Id: item.Id,
            DdtDocumentId: item.DdtDocumentId,
            VariantId: item.VariantId,
            Quantity: item.Quantity,
            UnitNetPrice: item.UnitNetPrice,
            VatRate: item.VatRate,
            LineNetTotal: item.LineNetTotal,
            LineVatTotal: item.LineVatTotal,
            LineGrossTotal: item.LineGrossTotal
        );
    }

    public static List<DdtDto> MapToListDto(IEnumerable<DdtDocument> ddts)
    {
        return ddts.Select(MapToDto).ToList();
    }
}
