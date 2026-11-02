using System.Globalization;
using Amazon.S3;
using Amazon.S3.Model;
using Mauw.Erp.Application.Services;
using Microsoft.Extensions.Options;

namespace Mauw.Erp.Infrastructure.Storage;

public sealed class S3InvoiceXmlStorage(Lazy<IAmazonS3> client, IOptions<S3StorageSettings> options)
    : ISdiDocumentStorage
{
    public async Task<string> UploadInvoiceXmlAsync(
        string fileName,
        byte[] xmlContent,
        CancellationToken cancellationToken = default)
        => await UploadXmlAsync("invoices", fileName, xmlContent, cancellationToken);

    public async Task<string> UploadCreditNoteXmlAsync(
        string fileName,
        byte[] xmlContent,
        CancellationToken cancellationToken = default)
        => await UploadXmlAsync("credit-notes", fileName, xmlContent, cancellationToken);

    private async Task<string> UploadXmlAsync(
        string folder,
        string fileName,
        byte[] xmlContent,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 50 ||
            fileName.Contains('/') || fileName.Contains('\\') ||
            !fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Il nome file XML SDI non è valido.", nameof(fileName));
        if (xmlContent.Length == 0)
            throw new ArgumentException("Il contenuto XML SDI non può essere vuoto.", nameof(xmlContent));

        var now = DateTime.UtcNow;
        var key = string.Create(CultureInfo.InvariantCulture,
            $"{folder}/{now:yyyy}/{now:MM}/{fileName}");
        using var stream = new MemoryStream(xmlContent, writable: false);
        await client.Value.PutObjectAsync(new PutObjectRequest
        {
            BucketName = options.Value.BucketName,
            Key = key,
            InputStream = stream,
            ContentType = "application/xml",
            AutoCloseStream = false,
            IfNoneMatch = "*"
        }, cancellationToken);
        return key;
    }

    public async Task<byte[]> GetInvoiceXmlAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
        => await GetXmlAsync(storageKey, cancellationToken);

    public async Task<byte[]> GetCreditNoteXmlAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
        => await GetXmlAsync(storageKey, cancellationToken);

    private async Task<byte[]> GetXmlAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        using var response = await client.Value.GetObjectAsync(
            options.Value.BucketName, storageKey, cancellationToken);
        await using var stream = response.ResponseStream;
        using var content = new MemoryStream();
        await stream.CopyToAsync(content, cancellationToken);
        return content.ToArray();
    }

    public Task DeleteInvoiceXmlAsync(
        string storageKey,
        CancellationToken cancellationToken = default) =>
        DeleteXmlAsync(storageKey, cancellationToken);

    public Task DeleteCreditNoteXmlAsync(
        string storageKey,
        CancellationToken cancellationToken = default) =>
        DeleteXmlAsync(storageKey, cancellationToken);

    private Task DeleteXmlAsync(string storageKey, CancellationToken cancellationToken) =>
        client.Value.DeleteObjectAsync(options.Value.BucketName, storageKey, cancellationToken);
}
