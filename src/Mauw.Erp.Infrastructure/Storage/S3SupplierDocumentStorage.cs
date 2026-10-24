using Amazon.S3;
using Amazon.S3.Model;
using Mauw.Erp.Application.Services;
using Microsoft.Extensions.Options;

namespace Mauw.Erp.Infrastructure.Storage;

public sealed class S3SupplierDocumentStorage(Lazy<IAmazonS3> client, IOptions<S3StorageSettings> options)
    : ISupplierDocumentStorage
{
    public async Task<string> UploadAsync(Guid id, Stream stream, string contentType, CancellationToken ct)
    {
        var extension = contentType switch
        {
            "application/pdf" => ".pdf",
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            _ => throw new ArgumentOutOfRangeException(nameof(contentType))
        };
        var key = $"supplier-delivery-notes/{id:N}/document{extension}";
        await client.Value.PutObjectAsync(new PutObjectRequest
        {
            BucketName = options.Value.BucketName, Key = key, InputStream = stream,
            ContentType = contentType, AutoCloseStream = false
        }, ct);
        return key;
    }

    public async Task<(Stream Stream, string ContentType)> DownloadAsync(string key, CancellationToken ct)
    {
        var response = await client.Value.GetObjectAsync(options.Value.BucketName, key, ct);
        return (response.ResponseStream, response.Headers.ContentType);
    }

    public Task DeleteAsync(string key, CancellationToken ct) =>
        client.Value.DeleteObjectAsync(options.Value.BucketName, key, ct);
}
