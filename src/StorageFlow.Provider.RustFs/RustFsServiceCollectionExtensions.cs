using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using StorageFlow.Abstractions.Configuration;
using StorageFlow.Abstractions.Exceptions;
using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Abstractions.Models;

namespace StorageFlow.Provider.RustFs;

/// <summary>
/// Extension methods for registering the RustFS provider with StorageFlow.
/// </summary>
public static class RustFsServiceCollectionExtensions
{
    /// <summary>
    /// Adds and configures the RustFS provider.
    /// Uses the AWS SDK for .NET (AWSSDK.S3) with a custom <c>ServiceURL</c>,
    /// as recommended by the official RustFS documentation.
    /// </summary>
    /// <param name="providers">The provider collection.</param>
    /// <param name="configure">Delegate to configure RustFS and its policies.</param>
    /// <returns>A handle that can mark RustFS as the default provider.</returns>
    public static ProviderRegistrationHandle UseRustFs(
        this ProviderCollectionBuilder providers,
        Action<RustFsRegistrationBuilder> configure)
    {
        var registration = new ProviderRegistrationBuilder();
        configure(new RustFsRegistrationBuilder(registration));
        return providers.Register(SFProvider.RustFs, registration);
    }
}

/// <summary>
/// Configures a RustFS provider registration and provider-level policies.
/// </summary>
public sealed class RustFsRegistrationBuilder
{
    private readonly ProviderRegistrationBuilder _registration;

    internal RustFsRegistrationBuilder(ProviderRegistrationBuilder registration)
    {
        _registration = registration;
    }

    /// <summary>Validation policy overrides for RustFS.</summary>
    public ValidationPolicyBuilder Validation => _registration.Validation;

    /// <summary>Naming policy overrides for RustFS.</summary>
    public NamingPolicyBuilder Naming => _registration.Naming;

    /// <summary>Presigned URL policy overrides for RustFS.</summary>
    public PresignedUrlPolicyBuilder PresignedUrls => _registration.PresignedUrls;

    /// <summary>Delivery URL policy overrides for RustFS.</summary>
    public DeliveryUrlPolicyBuilder DeliveryUrls => _registration.DeliveryUrls;

    /// <summary>Configures RustFS connection settings.</summary>
    public RustFsRegistrationBuilder Configure(Action<RustFsProviderOptions> configure)
    {
        var options = new RustFsProviderOptions
        {
            ServiceUrl = string.Empty,
            AccessKey = string.Empty,
            SecretKey = string.Empty
        };
        configure(options);

        var useHttpsForPresignedUrls = ResolvePresignedUrlProtocol(options.ServiceUrl);

        _registration.ConfigureProvider(_ =>
        {
            var credentials = new BasicAWSCredentials(options.AccessKey, options.SecretKey);
            var config = new AmazonS3Config
            {
                ServiceURL = options.ServiceUrl,
                ForcePathStyle = options.ForcePathStyle,
                AuthenticationRegion = options.Region
            };

            var client = new AmazonS3Client(credentials, config);
            return new RustFsStorageProvider(client, useHttpsForPresignedUrls);
        });

        return this;
    }

    private static bool ResolvePresignedUrlProtocol(string serviceUrl)
    {
        if (!Uri.TryCreate(serviceUrl, UriKind.Absolute, out var uri) ||
            !IsHttpScheme(uri.Scheme))
        {
            throw new StorageConfigurationException(
                "RustFS ServiceUrl must be an absolute HTTP or HTTPS URL.");
        }

        return uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHttpScheme(string scheme) =>
        scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
        scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
}
