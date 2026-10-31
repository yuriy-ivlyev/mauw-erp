using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;

namespace Mauw.Erp.Application.Mappers;

/// <summary>
/// Manual mapper for Invoice entities to DTOs without reflection.
/// </summary>
public static class InvoiceMapper
{
    public static InvoiceDto MapToDto(Invoice invoice, bool isOrderUnderAdjustment = false)
    {
        return new(
            Id: invoice.Id,
            InvoiceNumber: invoice.InvoiceNumber,
            ContactId: invoice.ContactId,
            OrderId: invoice.OrderId,
            ContactName: invoice.Contact?.BusinessName ?? string.Empty,
            InvoiceDate: invoice.InvoiceDate,
            TaxableTotal: invoice.TaxableTotal,
            VatTotal: invoice.VatTotal,
            GrossTotal: invoice.GrossTotal,
            ShippingCost: invoice.ShippingCost,
            ShippingVatRate: invoice.ShippingVatRate,
            ShippingNatureCode: invoice.ShippingNatureCode,
            SdiSubmittedAt: invoice.SdiSubmittedAt,
            PaidAmount: invoice.PaidAmount,
            BalanceDue: invoice.GrossTotal - invoice.PaidAmount,
            Status: invoice.Status,
            Items: invoice.Items.Select(MapItemToDto).ToList(),
            Payments: invoice.Payments.Select(MapPaymentToDto).ToList(),
            SdiStatus: invoice.SdiStatus.ToString(),
            IsOrderUnderAdjustment: isOrderUnderAdjustment
        );
    }

    private static InvoiceItemDto MapItemToDto(InvoiceItem item)
    {
        return new(
            Id: item.Id,
            InvoiceId: item.InvoiceId,
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
            Sku: item.Variant?.Barcode,
            ProductName: item.Variant?.Product?.Name,
            SizeName: item.Variant?.Size?.Name,
            ColorName: item.Variant?.Color?.Name
        );
    }

    private static PaymentDto MapPaymentToDto(Payment payment)
    {
        return new(
            Id: payment.Id,
            Amount: payment.Amount,
            PaymentDate: payment.PaymentDate,
            PaymentMethod: payment.PaymentMethod,
            TransactionDetails: payment.TransactionDetails
        );
    }

    public static List<InvoiceDto> MapToListDto(IEnumerable<Invoice> invoices)
    {
        return invoices.Select(invoice => MapToDto(invoice)).ToList();
    }
}