using Amazon.S3;
using Amazon.S3.Model;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Infrastructure.Storage;
using Microsoft.Extensions.Options;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

public sealed class SdiDocumentStorageTests
{
    [Fact]
    public async Task UploadUsesDatePartitioningAndPreventsReplacingAnArchivedXml()
    {
        var client = new Mock<IAmazonS3>(MockBehavior.Strict);
        PutObjectRequest? uploaded = null;
        byte[]? uploadedContent = null;
        client.Setup(value => value.PutObjectAsync(
                It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((request, _) =>
            {
                uploaded = request;
                using var content = new MemoryStream();
                request.InputStream.CopyTo(content);
                uploadedContent = content.ToArray();
            })
            .ReturnsAsync(new PutObjectResponse());
        var storage = CreateStorage(client.Object);
        var xml = "<invoice />"u8.ToArray();

        var key = await storage.UploadInvoiceXmlAsync(
            "IT01234567890_0261000001.xml", xml);

        Assert.Matches(
            @"^invoices/\d{4}/\d{2}/IT01234567890_0261000001\.xml$",
            key);
        Assert.NotNull(uploaded);
        Assert.Equal("sdi-documents", uploaded!.BucketName);
        Assert.Equal(key, uploaded.Key);
        Assert.Equal("*", uploaded.IfNoneMatch);
        Assert.Equal("application/xml", uploaded.ContentType);
        Assert.Equal(xml, uploadedContent);
    }

    [Fact]
    public async Task DownloadReturnsStoredBytesAndDeleteUsesTheSameBucketAndKey()
    {
        const string bucket = "sdi-documents";
        const string key = "invoices/2026/10/IT01234567890_0261000001.xml";
        var expected = "<invoice>frozen</invoice>"u8.ToArray();
        var client = new Mock<IAmazonS3>(MockBehavior.Strict);
        client.Setup(value => value.GetObjectAsync(bucket, key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new GetObjectResponse { ResponseStream = new MemoryStream(expected) });
        client.Setup(value => value.DeleteObjectAsync(bucket, key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteObjectResponse());
        var storage = CreateStorage(client.Object, bucket);

        var actual = await storage.GetInvoiceXmlAsync(key);
        await storage.DeleteInvoiceXmlAsync(key);

        Assert.Equal(expected, actual);
        client.VerifyAll();
    }

    [Fact]
    public async Task CreditNoteXmlUsesItsOwnDatePartitionAndStoragePrefix()
    {
        var client = new Mock<IAmazonS3>(MockBehavior.Strict);
        PutObjectRequest? uploaded = null;
        client.Setup(value => value.PutObjectAsync(
                It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((request, _) => uploaded = request)
            .ReturnsAsync(new PutObjectResponse());
        var storage = CreateStorage(client.Object);

        var key = await storage.UploadCreditNoteXmlAsync(
            "IT01234567890_0261000001.xml", "<credit-note />"u8.ToArray());

        Assert.Matches(
            @"^credit-notes/\d{4}/\d{2}/IT01234567890_0261000001\.xml$",
            key);
        Assert.Equal(key, uploaded!.Key);
        Assert.Equal("application/xml", uploaded.ContentType);
        Assert.Equal("*", uploaded.IfNoneMatch);
    }

    private static S3InvoiceXmlStorage CreateStorage(IAmazonS3 client, string bucket = "sdi-documents") =>
        new(new Lazy<IAmazonS3>(() => client),
            Options.Create(new S3StorageSettings { BucketName = bucket }));
}
