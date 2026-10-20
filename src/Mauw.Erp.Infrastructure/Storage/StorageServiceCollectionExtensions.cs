using Amazon.Runtime;
using Amazon.S3;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Mauw.Erp.Infrastructure.Storage;

public static class StorageServiceCollectionExtensions
{
    public static IServiceCollection AddImageStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<S3StorageSettings>(configuration.GetSection(S3StorageSettings.Section));

        // The client is built lazily so a missing S3 configuration fails on first use, not at startup.
        services.AddSingleton<IAmazonS3>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<S3StorageSettings>>().Value;
            if (string.IsNullOrWhiteSpace(settings.Endpoint) ||
                string.IsNullOrWhiteSpace(settings.AccessKey) ||
                string.IsNullOrWhiteSpace(settings.SecretKey) ||
                string.IsNullOrWhiteSpace(settings.BucketName))
                throw new InvalidOperationException(
                    "S3Storage configuration is incomplete (Endpoint, AccessKey, SecretKey, BucketName).");

            return new AmazonS3Client(
                new BasicAWSCredentials(settings.AccessKey, settings.SecretKey),
                new AmazonS3Config
                {
                    ServiceURL = settings.Endpoint,
                    AuthenticationRegion = settings.Region,
                    ForcePathStyle = settings.ForcePathStyle,
                    // Recent SDK default checksums are not supported by every S3-compatible backend.
                    RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                    ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
                });
        });

        services.AddSingleton<IImageStorageService, S3ImageStorageService>();
        services.AddScoped<IProductVariantImageRepository, ProductVariantImageRepository>();

        return services;
    }
}
