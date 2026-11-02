namespace Mauw.Erp.Domain.Enums;

public static class CreditNoteReasons
{
    public const string GoodsReturn = nameof(GoodsReturn);
    public const string PriceAdjustment = nameof(PriceAdjustment);
    public const string CommercialAllowance = nameof(CommercialAllowance);
    public const string FullCancellation = nameof(FullCancellation);

    public static bool IsSupported(string? reason) =>
        reason is GoodsReturn or PriceAdjustment or CommercialAllowance or FullCancellation;

    public static string GetLabel(string? reason) => reason switch
    {
        GoodsReturn => "Reso merce",
        PriceAdjustment => "Rettifica prezzo",
        CommercialAllowance => "Abbuono commerciale",
        FullCancellation => "Storno totale",
        _ => "Nota di credito"
    };
}
