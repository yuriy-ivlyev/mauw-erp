namespace Mauw.Erp.Domain.Entities;

public interface ICommercialDocumentLine
{
    decimal Quantity { get; }
    decimal UnitNetPrice { get; set; }
    decimal DiscountPercent { get; set; }
    decimal VatRate { get; }
    string? NatureCode { get; set; }
    decimal LineNetTotal { get; set; }
    decimal? LineVatTotal { get; set; }
    decimal? LineGrossTotal { get; set; }
}
