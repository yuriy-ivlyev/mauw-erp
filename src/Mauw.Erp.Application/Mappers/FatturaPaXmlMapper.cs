using System.Globalization;
using System.Xml.Linq;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos.Pdf;

namespace Mauw.Erp.Application.Mappers;

public static class FatturaPaXmlMapper
{
    public static IReadOnlyList<XElement> BuildRecipientAddressElements(
        string? sdiCode,
        string? pecEmail,
        XNamespace? fatturaPaNamespace = null)
    {
        var ns = fatturaPaNamespace ?? XNamespace.None;
        var destinationCode = string.IsNullOrWhiteSpace(sdiCode) ? "0000000" : sdiCode.Trim();
        var isDefaultCode = destinationCode.Equals("0000000", StringComparison.Ordinal);
        var elements = new List<XElement>
        {
            new(ns + "CodiceDestinatario", destinationCode)
        };

        if (isDefaultCode && !string.IsNullOrWhiteSpace(pecEmail))
            elements.Add(new XElement(ns + "PECDestinatario", pecEmail.Trim()));

        return elements;
    }

    public static OperationResult<XElement> BuildDatiPagamento(
        InvoicePdfPaymentInfo payment,
        XNamespace? fatturaPaNamespace = null)
    {
        if (string.IsNullOrWhiteSpace(payment.PaymentConditionCode))
            return OperationResult<XElement>.Fail("Il codice delle condizioni di pagamento FatturaPA è obbligatorio.");
        if (string.IsNullOrWhiteSpace(payment.PaymentMethod))
            return OperationResult<XElement>.Fail("La modalità di pagamento FatturaPA è obbligatoria.");
        if (payment.Amount is not { } amount || amount < 0m || decimal.Round(amount, 2, MidpointRounding.AwayFromZero) != amount)
            return OperationResult<XElement>.Fail("L'importo del pagamento deve essere valorizzato e avere al massimo due decimali.");

        var ns = fatturaPaNamespace ?? XNamespace.None;
        var detail = new XElement(ns + "DettaglioPagamento",
            new XElement(ns + "ModalitaPagamento", payment.PaymentMethod.Trim()));

        if (payment.DueDate is { } dueDate)
            detail.Add(new XElement(ns + "DataScadenzaPagamento",
                dueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        detail.Add(new XElement(ns + "ImportoPagamento", amount.ToString("0.00", CultureInfo.InvariantCulture)));

        if (!string.IsNullOrWhiteSpace(payment.BankName))
            detail.Add(new XElement(ns + "IstitutoFinanziario", payment.BankName.Trim()));
        if (!string.IsNullOrWhiteSpace(payment.Iban))
            detail.Add(new XElement(ns + "IBAN", payment.Iban.Trim()));
        if (!string.IsNullOrWhiteSpace(payment.Swift))
            detail.Add(new XElement(ns + "BIC", payment.Swift.Trim()));

        return OperationResult<XElement>.Ok(new XElement(ns + "DatiPagamento",
            new XElement(ns + "CondizioniPagamento", payment.PaymentConditionCode.Trim()),
            detail));
    }
}
