using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Application.Mappers;

/// <summary>
/// Manual mapper for Order entities to DTOs without reflection.
/// </summary>
public static class OrderMapper
{
    public static OrderDto MapToDto(Order order, OrderLinkedDocumentsDto? linked = null)
    {
        return new(
            Id: order.Id,
            OrderNumber: order.OrderNumber,
            ContactId: order.ContactId,
            ContactName: order.Contact?.BusinessName ?? "",
            OrderDate: order.OrderDate,
            CustomerDiscountRate: order.CustomerDiscountRate,
            Status: order.Status.ToString(),
            TaxableTotal: order.TaxableTotal,
            VatTotal: order.VatTotal,
            GrossTotal: order.GrossTotal,
            OrderType: order.OrderType.ToString(),
            Items: order.Items.Select(MapItemToDto).ToList(),
            LinkedDocuments: linked
        );
    }

    private static OrderItemDto MapItemToDto(OrderItem item)
    {
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
            DiscountPercent: item.DiscountPercent
        );
    }

    public static List<OrderDto> MapToListDto(IEnumerable<Order> orders)
    {
        return orders.Select(order => MapToDto(order)).ToList();
    }
}
