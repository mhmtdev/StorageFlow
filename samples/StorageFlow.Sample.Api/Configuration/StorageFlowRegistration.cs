using StorageFlow.Abstractions.Models;
using StorageFlow.Core.DependencyInjection;
using StorageFlow.Provider.Minio;
using StorageFlow.Provider.S3;
using StorageFlow.Sample.Api.Policies;

namespace StorageFlow.Sample.Api.Configuration;

public static class StorageFlowRegistration
{
    public static IServiceCollection AddStorageFlowSample(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var backend = configuration
            .GetSection(StorageBackendOptions.SectionName)
            .Get<StorageBackendOptions>() ?? new StorageBackendOptions();

        new StorageBackendConfigurationValidator().Validate(backend);
        services.AddSingleton(backend);

        services.AddStorageFlow(options =>
        {
            options.Validation.AddPolicy<DocumentsPolicy>(policy =>
            {
                policy.MaxFileSizeBytes = 10 * 1024 * 1024;
                policy.AllowedExtensions = [".pdf", ".zip"];
                policy.AllowedMimeTypes = ["application/pdf", "application/zip"];
                policy.RequireValidSignature = true;
            });

            options.Naming
                .AddPolicy<MediaNaming>(policy =>
                    policy.UsePattern("{yyyy}/{MM}/{slug}-{guid}{ext}"))
                .AsDefault();

            options.PresignedUrls.AddPolicy<DownloadPolicy>(policy =>
            {
                policy.Expiration = TimeSpan.FromMinutes(15);
                policy.HttpMethod = HttpMethod.Get;
            });

            options.DeliveryUrls.AddPolicy<PublicAssetDelivery>(policy => policy
                .UseCdn(backend.Cdn.BaseUrl)
                .WithPathPrefix(backend.Cdn.PathPrefix)
                .IncludeBucket(backend.Cdn.IncludeBucket));

            if (backend.Minio.Enabled)
            {
                options.Providers.UseMinio(minio =>
                {
                    minio.Configure(config =>
                    {
                        config.Endpoint = backend.Minio.Endpoint;
                        config.AccessKey = backend.Minio.AccessKey;
                        config.SecretKey = backend.Minio.SecretKey;
                        config.UseSSL = backend.Minio.UseSsl;
                        config.Region = backend.Minio.Region;
                    });
                }).AsDefault();
            }
            else if (backend.S3.Enabled)
            {
                options.Providers.UseS3(s3 =>
                {
                    s3.Configure(config =>
                    {
                        config.Region = backend.S3.Region;
                        config.AccessKey = backend.S3.AccessKey;
                        config.SecretKey = backend.S3.SecretKey;
                        config.SessionToken = backend.S3.SessionToken;
                    });
                }).AsDefault();
            }
        });

        return services;
    }
}
