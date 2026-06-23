using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using StorageFlow.Abstractions.Configuration;
using StorageFlow.Abstractions.Exceptions;
using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Abstractions.Models;

namespace StorageFlow.Provider.S3;

/// <summary>
/// Extension methods for registering the AWS S3 provider with StorageFlow.
/// </summary>
public static class S3ServiceCollectionExtensions
{
    /// <summary>
    /// Adds and configures the AWS S3 provider.
    /// </summary>
    /// <param name="providers">The provider collection.</param>
    /// <param name="configure">Delegate to configure S3 and its policies.</param>
    /// <returns>A handle that can mark S3 as the default provider.</returns>
    public static ProviderRegistrationHandle UseS3(
        this ProviderCollectionBuilder providers,
        Action<S3RegistrationBuilder> configure)
    {
        var registration = new ProviderRegistrationBuilder();
        configure(new S3RegistrationBuilder(registration));
        return providers.Register(SFProvider.S3, registration);
    }
}

/// <summary>
/// Configures an AWS S3 provider registration and provider-level policies.
/// </summary>
public sealed class S3RegistrationBuilder
{
    private readonly ProviderRegistrationBuilder _registration;

    internal S3RegistrationBuilder(ProviderRegistrationBuilder registration)
    {
        _registration = registration;
    }

    /// <summary>Validation policy overrides for S3.</summary>
    public ValidationPolicyBuilder Validation => _registration.Validation;

    /// <summary>Naming policy overrides for S3.</summary>
    public NamingPolicyBuilder Naming => _registration.Naming;

    /// <summary>Presigned URL policy overrides for S3.</summary>
    public PresignedUrlPolicyBuilder PresignedUrls => _registration.PresignedUrls;

    /// <summary>Delivery URL policy overrides for S3.</summary>
    public DeliveryUrlPolicyBuilder DeliveryUrls => _registration.DeliveryUrls;

    /// <summary>Configures AWS S3 connection settings.</summary>
    public S3RegistrationBuilder Configure(Action<S3ProviderOptions> configure)
    {
        var options = new S3ProviderOptions
        {
            Region = string.Empty
        };
        configure(options);

        var hasAccessKey = !string.IsNullOrWhiteSpace(options.AccessKey);
        var hasSecretKey = !string.IsNullOrWhiteSpace(options.SecretKey);
        if (hasAccessKey != hasSecretKey)
        {
            throw new StorageConfigurationException(
                "S3 AccessKey and SecretKey must be supplied together, or both omitted to use the AWS default credential chain.");
        }

        if (!string.IsNullOrWhiteSpace(options.SessionToken) && !hasAccessKey)
        {
            throw new StorageConfigurationException(
                "S3 SessionToken requires AccessKey and SecretKey.");
        }

        var useHttpsForPresignedUrls = ResolvePresignedUrlProtocol(options.ServiceUrl);

        _registration.ConfigureProvider(_ =>
        {
            var config = new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region)
            };

            if (!string.IsNullOrWhiteSpace(options.ServiceUrl))
            {
                config.ServiceURL = options.ServiceUrl;
                config.ForcePathStyle = true;
            }

            AmazonS3Client client;
            if (!hasAccessKey)
            {
                client = new AmazonS3Client(config);
            }
            else if (!string.IsNullOrWhiteSpace(options.SessionToken))
            {
                client = new AmazonS3Client(
                    new SessionAWSCredentials(
                        options.AccessKey!,
                        options.SecretKey!,
                        options.SessionToken),
                    config);
            }
            else
            {
                client = new AmazonS3Client(
                    new BasicAWSCredentials(options.AccessKey!, options.SecretKey!),
                    config);
            }

            return new S3StorageProvider(client, useHttpsForPresignedUrls);
        });

        return this;
    }

    private static bool ResolvePresignedUrlProtocol(string? serviceUrl)
    {
        if (string.IsNullOrWhiteSpace(serviceUrl))
            return true;

        if (!Uri.TryCreate(serviceUrl, UriKind.Absolute, out var uri) ||
            !IsHttpScheme(uri.Scheme))
        {
            throw new StorageConfigurationException(
                "S3 ServiceUrl must be an absolute HTTP or HTTPS URL.");
        }

        return uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHttpScheme(string scheme) =>
        scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
        scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
}
