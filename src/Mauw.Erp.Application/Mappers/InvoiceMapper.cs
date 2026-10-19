using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Application.Mappers;

/// <summary>
/// Manual mapper for Invoice entities to DTOs without reflection.
/// </summary>
public static class InvoiceMapper
{
    public static InvoiceDto MapToDto(Invoice invoice)
    {
        return new(
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
            PricingSnapshot: PricingSnapshotMapper.Map(item.PricingSnapshot)
        );
    }

    private static PaymentDto MapPaymentToDto(Payment payment)
    {
        return new(
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