using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Enums;
using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Application.Mappers;

/// <summary>
/// Manual mapper for Order entities to DTOs without reflection.
/// </summary>
public static class OrderMapper
{
    public static OrderDto MapToDto(
        Order order,
        OrderLinkedDocumentsDto? linked = null,
        IReadOnlyDictionary<Guid, int>? shippedQuantities = null,
        bool canEditInvoice = false)
    {
        return new(
            Id: order.Id,
            OrderNumber: order.OrderNumber,
            ContactId: order.ContactId,
            ContactName: order.Contact?.BusinessName ?? "",
            OrderDate: order.OrderDate,
            CustomerDiscountRate: order.CustomerDiscountRate,
            Status: (OrderStatusEnum)order.Status,
            TaxableTotal: order.TaxableTotal,
            VatTotal: order.VatTotal,
            GrossTotal: order.GrossTotal,
            OrderType: order.OrderType.ToString(),
            FulfillmentType: order.FulfillmentType.ToString(),
            Items: order.Items.Select(item => MapItemToDto(item, shippedQuantities)).ToList(),
            LinkedDocuments: linked,
            StatusDescription: OrderPermittedActionsPolicy.GetStatusDescription(order.Status),
            IsReadOnly: order.Status != Domain.Enums.OrderStatus.Draft,
            PermittedActions: OrderPermittedActionsPolicy.GetFor(order),
            CanEditInvoice: canEditInvoice,
            InternalNotes: order.InternalNotes,
            ShippingCost: order.ShippingCost,
            ShippingVatRate: order.ShippingVatRate,
            ShippingNatureCode: order.ShippingNatureCode
        );
    }

    private static OrderItemDto MapItemToDto(
        OrderItem item,
        IReadOnlyDictionary<Guid, int>? shippedQuantities)
    {
        var shippedQuantity = shippedQuantities?.GetValueOrDefault(item.Id) ?? item.QuantityShipped;
        return new(
            Id: item.Id,
            OrderId: item.OrderId,
            VariantId: item.VariantId,
            Quantity: item.Quantity,
            UnitNetPrice: item.UnitNetPrice,
            VatRate: item.VatRate,
            LineNetTotal: item.LineNetTotal,
            LineVatTotal: null,
            LineGrossTotal: null,
            NatureCode: item.NatureCode,
            DiscountPercent: item.DiscountPercent,
            PricingSnapshot: PricingSnapshotMapper.Map(item.PricingSnapshot),
            ShippedQuantity: shippedQuantity,
            RemainingQuantity: item.IsBillingOnly || item.IsExcludedFromInvoice
                ? 0
                : Math.Max(0, item.Quantity - shippedQuantity),
            IsBillingOnly: item.IsBillingOnly,
            IsExcludedFromInvoice: item.IsExcludedFromInvoice,
            Sku: item.Variant?.Barcode,
            ProductName: item.Variant?.Product?.Name,
            SizeName: item.Variant?.Size?.Name,
            ColorName: item.Variant?.Color?.Name
        );
    }

    public static List<OrderDto> MapToListDto(IEnumerable<Order> orders)
    {
        return orders.Select(order => MapToDto(order)).ToList();
    }
}
