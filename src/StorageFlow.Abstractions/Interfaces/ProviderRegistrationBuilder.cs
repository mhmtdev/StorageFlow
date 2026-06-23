using StorageFlow.Abstractions.Configuration;
using StorageFlow.Abstractions.Models;

namespace StorageFlow.Abstractions.Interfaces;

/// <summary>
/// Holds policies and the provider factory assembled by a first-party provider package.
/// Provider-level policy overrides are configured through
/// <see cref="Validation"/>, <see cref="Naming"/>, <see cref="PresignedUrls"/>,
/// and <see cref="DeliveryUrls"/>.
/// </summary>
internal sealed class ProviderRegistrationBuilder
{
    private int _configurationCount;

    internal ProviderRegistrationBuilder()
    {
    }

    /// <summary>Validation policy overrides for this provider.</summary>
    public ValidationPolicyBuilder Validation { get; } = new();

    /// <summary>Naming policy overrides for this provider.</summary>
    public NamingPolicyBuilder Naming { get; } = new();

    /// <summary>Presigned URL policy overrides for this provider.</summary>
    public PresignedUrlPolicyBuilder PresignedUrls { get; } = new();

    /// <summary>Delivery URL policy overrides for this provider.</summary>
    public DeliveryUrlPolicyBuilder DeliveryUrls { get; } = new();

    internal Func<IServiceProvider, IStorageProvider>? ProviderFactory { get; private set; }

    internal void ConfigureProvider(Func<IServiceProvider, IStorageProvider> factory)
    {
        _configurationCount++;
        ProviderFactory = factory;
    }

    internal void ValidateConfigured(StorageProviderToken token)
    {
        if (_configurationCount == 0 || ProviderFactory is null)
            throw new Exceptions.StorageConfigurationException(
                $"Provider '{token}' must be configured exactly once.");

        if (_configurationCount > 1)
            throw new Exceptions.StorageConfigurationException(
                $"Provider '{token}' was configured more than once.");
    }
}
