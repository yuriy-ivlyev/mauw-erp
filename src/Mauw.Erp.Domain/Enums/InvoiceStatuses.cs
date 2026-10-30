namespace Mauw.Erp.Domain.Enums;

public static class InvoiceStatuses
{
    public const string Draft = nameof(Draft);
    public const string Confirmed = nameof(Confirmed);
    public const string PartiallyPaid = nameof(PartiallyPaid);
    public const string Paid = nameof(Paid);

    public static bool IsSupported(string status) =>
        status is Confirmed or PartiallyPaid or Paid;
}
