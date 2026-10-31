using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.Application.Services;

/// <summary>
/// Loads the customer of a document and applies <see cref="CustomerTaxPolicy"/>: the only server-side entry
/// point used by order handlers to validate or default the VAT classification of lines and shipping charges.
/// </summary>
internal static class CustomerTaxClassification
{
    public static async Task<OperationResult<FiscalClassification>> ClassifyAsync(
        this IUnitOfWork unitOfWork, Guid contactId, decimal? vatRate, string? natureCode)
    {
        var contact = await unitOfWork.Contacts.GetByIdAsync(contactId);
        if (contact is null)
            return OperationResult<FiscalClassification>.Fail("Contact not found");

        var classification = CustomerTaxPolicy.Classify(contact.CountryCode, vatRate, natureCode);
        return classification.Success
            ? OperationResult<FiscalClassification>.Ok(classification.Data!)
            : OperationResult<FiscalClassification>.Fail(classification.Error);
    }
}
