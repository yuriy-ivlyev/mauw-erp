using Amazon.S3;
using Amazon.S3.Model;
using Mauw.Erp.Application.Services;
using Microsoft.Extensions.Options;

namespace Mauw.Erp.Infrastructure.Storage;

public sealed class S3EmployeeDocumentStorage(
    Lazy<IAmazonS3> client,
    IOptions<S3StorageSettings> options) : IEmployeeDocumentStorage
{
    public async Task<string> UploadAsync(
        Guid employeeId,
        Guid documentId,
        Stream stream,
        string contentType,
        CancellationToken cancellationToken)
    {
        var extension = contentType switch
        {
            "application/pdf" => ".pdf",
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            _ => throw new ArgumentOutOfRangeException(nameof(contentType))
        };
        var key = $"employees/{employeeId:N}/documents/{documentId:N}/{Guid.NewGuid():N}{extension}";
        await client.Value.PutObjectAsync(new PutObjectRequest
        {
            BucketName = options.Value.BucketName,
            Key = key,
            InputStream = stream,
            ContentType = contentType,
            AutoCloseStream = false
        }, cancellationToken);
        return key;
    }

    public async Task<(Stream Stream, string ContentType)> DownloadAsync(
        string key,
        CancellationToken cancellationToken)
    {
        var response = await client.Value.GetObjectAsync(
            options.Value.BucketName, key, cancellationToken);
        return (response.ResponseStream, response.Headers.ContentType);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        client.Value.DeleteObjectAsync(options.Value.BucketName, key, cancellationToken);
}
