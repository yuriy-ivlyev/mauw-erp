using Amazon;
using Amazon.Runtime;
using Amazon.Textract;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Mauw.Erp.Infrastructure.Ocr;

public static class SupplierDocumentOcrServiceCollectionExtensions
{
    private const string DefaultTextractRegion = "eu-central-1";

    public static IServiceCollection AddSupplierDocumentOcr(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IAmazonTextract>(_ =>
        {
            var regionName = configuration["AWS:TextractRegion"];
            if (string.IsNullOrWhiteSpace(regionName))
                regionName = Environment.GetEnvironmentVariable("AWS_TEXTRACT_REGION");
            if (string.IsNullOrWhiteSpace(regionName))
                regionName = DefaultTextractRegion;

            var accessKeyId = ResolveCredential(
                configuration["AWS:AccessKeyId"], "/run/secrets/aws_access_key_id", "AWS_ACCESS_KEY_ID");
            var secretAccessKey = ResolveCredential(
                configuration["AWS:SecretAccessKey"], "/run/secrets/aws_secret_access_key", "AWS_SECRET_ACCESS_KEY");
            var hasAccessKeyId = !string.IsNullOrWhiteSpace(accessKeyId);
            var hasSecretAccessKey = !string.IsNullOrWhiteSpace(secretAccessKey);

            if (hasAccessKeyId != hasSecretAccessKey)
                throw new AmazonClientException(
                    "AWS configuration must include both AccessKeyId and SecretAccessKey.");

            var clientConfig = new AmazonTextractConfig();
            if (!string.IsNullOrWhiteSpace(regionName))
                clientConfig.RegionEndpoint = RegionEndpoint.GetBySystemName(regionName);

            var credentials = hasAccessKeyId
                ? new BasicAWSCredentials(accessKeyId!, secretAccessKey!)
                : null;

            return credentials is null
                ? new AmazonTextractClient(clientConfig)
                : new AmazonTextractClient(credentials, clientConfig);
        });
        services.AddSingleton(sp => new Lazy<IAmazonTextract>(
            () => sp.GetRequiredService<IAmazonTextract>()));
        services.AddScoped<ISupplierDocumentOcrProvider, AmazonTextractDocumentOcrProvider>();
        services.AddScoped<ISupplierDocumentOcrAliasService, SupplierDocumentOcrAliasService>();
        services.AddScoped<ISupplierDocumentOcrAliasService, SupplierDocumentOcrAliasService>();
        return services;
    }

    private static string? ResolveCredential(
        string? configuredValue,
        string secretFilePath,
        string environmentVariable)
    {
        if (!string.IsNullOrWhiteSpace(configuredValue))
            return configuredValue;

        if (File.Exists(secretFilePath))
        {
            var secretValue = File.ReadAllText(secretFilePath).Trim();
            if (!string.IsNullOrWhiteSpace(secretValue))
                return secretValue;
        }

        return Environment.GetEnvironmentVariable(environmentVariable);
    }
}
