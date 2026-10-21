using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Mauw.Erp.Application.Services;
using Microsoft.Extensions.Options;

namespace Mauw.Erp.Infrastructure.Storage;

public sealed class S3ImageStorageService(Lazy<IAmazonS3> client, IOptions<S3StorageSettings> options) : IImageStorageService
{
    private readonly string _bucket = options.Value.BucketName;
    private IAmazonS3 s3 => client.Value;

    public async Task<string> UploadImageAsync(
        Guid targetId, Stream imageStream, string fileName, string contentType, CancellationToken ct = default)
    {
        var key = ImageStorageKeys.Build(targetId, fileName);

        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = imageStream,
            ContentType = contentType,
            AutoCloseStream = false
        }, ct);

        return key;
    }

    public async Task<(Stream Stream, string ContentType)> GetImageStreamAsync(
        string filePath, CancellationToken ct = default)
    {
        try
        {
            var response = await s3.GetObjectAsync(_bucket, filePath, ct);
            return (response.ResponseStream, response.Headers.ContentType);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new FileNotFoundException("Image not found.", filePath, ex);
        }
    }

    public Task DeleteImageAsync(string filePath, CancellationToken ct = default) =>
        s3.DeleteObjectAsync(_bucket, filePath, ct);
}
