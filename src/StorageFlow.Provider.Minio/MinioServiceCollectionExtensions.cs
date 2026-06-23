using Amazon.Runtime;
using Amazon.S3;
using global::Minio;
using StorageFlow.Abstractions.Configuration;
using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Abstractions.Models;

namespace StorageFlow.Provider.Minio;

/// <summary>
/// Extension methods for registering the MinIO provider with StorageFlow.
/// </summary>
public static class MinioServiceCollectionExtensions
{
    /// <summary>
    /// Adds and configures the MinIO provider.
    /// </summary>
    /// <param name="providers">The provider collection.</param>
    /// <param name="configure">Delegate to configure MinIO and its policies.</param>
    /// <returns>A handle that can mark MinIO as the default provider.</returns>
    public static ProviderRegistrationHandle UseMinio(
        this ProviderCollectionBuilder providers,
        Action<MinioRegistrationBuilder> configure)
    {
        var registration = new ProviderRegistrationBuilder();
        configure(new MinioRegistrationBuilder(registration));
        return providers.Register(SFProvider.Minio, registration);
    }
}

/// <summary>
/// Configures a MinIO provider registration and provider-level policies.
/// </summary>
public sealed class MinioRegistrationBuilder
{
    private readonly ProviderRegistrationBuilder _registration;

    internal MinioRegistrationBuilder(ProviderRegistrationBuilder registration)
    {
        _registration = registration;
    }

    /// <summary>Validation policy overrides for MinIO.</summary>
    public ValidationPolicyBuilder Validation => _registration.Validation;

    /// <summary>Naming policy overrides for MinIO.</summary>
    public NamingPolicyBuilder Naming => _registration.Naming;

    /// <summary>Presigned URL policy overrides for MinIO.</summary>
    public PresignedUrlPolicyBuilder PresignedUrls => _registration.PresignedUrls;

    /// <summary>Delivery URL policy overrides for MinIO.</summary>
    public DeliveryUrlPolicyBuilder DeliveryUrls => _registration.DeliveryUrls;

    /// <summary>Configures MinIO connection settings.</summary>
    public MinioRegistrationBuilder Configure(Action<MinioProviderOptions> configure)
    {
        var options = new MinioProviderOptions
        {
            Endpoint = string.Empty,
            AccessKey = string.Empty,
            SecretKey = string.Empty
        };
        configure(options);

        _registration.ConfigureProvider(_ =>
        {
            var client = new MinioClient()
                .WithEndpoint(options.Endpoint)
                .WithCredentials(options.AccessKey, options.SecretKey)
                .WithSSL(options.UseSSL)
                .Build();

            var serviceUrl = Uri.TryCreate(
                options.Endpoint,
                UriKind.Absolute,
                out var absoluteEndpoint) &&
                absoluteEndpoint.Scheme is "http" or "https"
                    ? absoluteEndpoint.ToString()
                    : $"{(options.UseSSL ? "https" : "http")}://{options.Endpoint}";
            var uploadClient = new AmazonS3Client(
                new BasicAWSCredentials(options.AccessKey, options.SecretKey),
                new AmazonS3Config
                {
                    ServiceURL = serviceUrl,
                    ForcePathStyle = true,
                    AuthenticationRegion = options.Region
                });

            return new MinioStorageProvider(client, uploadClient);
        });

        return this;
    }
}
