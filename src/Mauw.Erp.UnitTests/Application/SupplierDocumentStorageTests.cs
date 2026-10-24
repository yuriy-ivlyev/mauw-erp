using Amazon.Runtime;
using Amazon.S3;
using Mauw.Erp.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace Mauw.Erp.UnitTests.Application;

public class SupplierDocumentStorageTests
{
    [S3Fact]
    public async Task OriginalBytesAndContentTypeRoundTripThroughS3AndAreDeleted()
    {
        using var client = new AmazonS3Client(
            new BasicAWSCredentials(Environment.GetEnvironmentVariable("MAUW_TEST_S3_ACCESS_KEY"),
                Environment.GetEnvironmentVariable("MAUW_TEST_S3_SECRET_KEY")),
            new AmazonS3Config
            {
                ServiceURL = Environment.GetEnvironmentVariable("MAUW_TEST_S3_ENDPOINT"),
                AuthenticationRegion = "us-east-1", ForcePathStyle = true,
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
            });
        var bucket = $"supplier-test-{Guid.NewGuid():N}";
        await client.PutBucketAsync(bucket);
        var storage = new S3SupplierDocumentStorage(new(() => client), Options.Create(new S3StorageSettings { BucketName = bucket }));
        string? key = null;
        try
        {
            var original = "%PDF-1.7 supplier document roundtrip"u8.ToArray();
            using var input = new MemoryStream(original);
            var id = Guid.NewGuid();
            key = await storage.UploadAsync(id, input, "application/pdf", default);
            Assert.Equal($"supplier-delivery-notes/{id:N}/document.pdf", key);
            var file = await storage.DownloadAsync(key, default);
            await using var stream = file.Stream;
            using var output = new MemoryStream();
            await stream.CopyToAsync(output);
            Assert.Equal("application/pdf", file.ContentType);
            Assert.Equal(original, output.ToArray());
            await storage.DeleteAsync(key, default);
            key = null;
            var remaining = await client.ListObjectsV2Async(new() { BucketName = bucket });
            Assert.True(remaining.S3Objects is null or { Count: 0 });
        }
        finally
        {
            if (key is not null) await storage.DeleteAsync(key, default);
            await client.DeleteBucketAsync(bucket);
        }
    }

    private sealed class S3FactAttribute : FactAttribute
    {
        public S3FactAttribute()
        {
            if (new[] { "MAUW_TEST_S3_ENDPOINT", "MAUW_TEST_S3_ACCESS_KEY", "MAUW_TEST_S3_SECRET_KEY" }
                .Any(name => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))))
                Skip = "Set MAUW_TEST_S3_ENDPOINT, MAUW_TEST_S3_ACCESS_KEY and MAUW_TEST_S3_SECRET_KEY for an isolated S3 roundtrip.";
        }
    }
}
