using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Application.Mappers;

public static class CreditNoteMapper
{
    public static CreditNoteSummaryDto ToSummaryDto(CreditNote note) =>
        new(
            note.Id,
            note.CreditNoteNumber,
            note.CreditNoteDate,
            note.TaxableTotal,
            note.VatTotal,
            note.GrossTotal,
            note.Reason,
            note.SdiStatus.ToString(),
            note.AllocatedAmount,
            note.RemainingCredit);
}
