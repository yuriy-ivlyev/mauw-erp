using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Application.Mappers;

/// <summary>
/// Manual mapper for DdtDocument entities to DTOs without reflection.
/// </summary>
public static class DdtMapper
{
    public static DdtDto MapToDto(DdtDocument ddt)
    {
        return new(
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
        return new(
            Id: item.Id,
            DdtDocumentId: item.DdtDocumentId,
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

    public static List<DdtDto> MapToListDto(IEnumerable<DdtDocument> ddts)
    {
        return ddts.Select(MapToDto).ToList();
    }
}