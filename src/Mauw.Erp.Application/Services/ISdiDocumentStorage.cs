namespace Mauw.Erp.Application.Services;

public interface ISdiDocumentStorage
{
    Task<string> UploadInvoiceXmlAsync(
        string fileName,
        byte[] xmlContent,
        CancellationToken cancellationToken = default);

    Task<byte[]> GetInvoiceXmlAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    Task DeleteInvoiceXmlAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    Task<string> UploadCreditNoteXmlAsync(
        string fileName,
        byte[] xmlContent,
        CancellationToken cancellationToken = default);

    Task<byte[]> GetCreditNoteXmlAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    Task DeleteCreditNoteXmlAsync(
        string storageKey,
        CancellationToken cancellationToken = default);
}
