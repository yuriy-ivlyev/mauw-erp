namespace Mauw.Erp.Infrastructure.Storage;

public sealed class S3StorageSettings
{
    public const string Section = "S3Storage";

    public string Endpoint { get; set; } = "";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string BucketName { get; set; } = "";
    public string Region { get; set; } = "us-east-1";
    public bool ForcePathStyle { get; set; } = true;
}
