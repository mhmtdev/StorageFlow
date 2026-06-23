using StorageFlow.Abstractions.Configuration;
using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Abstractions.Models;

namespace StorageFlow.Testing;

/// <summary>
/// Test-only provider tokens.
/// </summary>
public static class SFTestProvider
{
    /// <summary>In-memory provider token.</summary>
    public static StorageProviderToken InMemory { get; } = new("memory");

    /// <summary>Token used to verify provider-name mismatch handling.</summary>
    public static StorageProviderToken Mismatched { get; } = new("mismatched");
}

/// <summary>
/// Test-only provider registration extensions.
/// </summary>
public static class InMemoryProviderExtensions
{
    /// <summary>Adds the in-memory provider used by StorageFlow tests.</summary>
    public static ProviderRegistrationHandle UseInMemory(
        this ProviderCollectionBuilder providers,
        Action<InMemoryRegistrationBuilder>? configure = null)
    {
        var registration = new ProviderRegistrationBuilder();
        registration.ConfigureProvider(_ => new InMemoryStorageProvider());
        configure?.Invoke(new InMemoryRegistrationBuilder(registration));
        return providers.Register(SFTestProvider.InMemory, registration);
    }

    /// <summary>Adds a deliberately mismatched provider registration for tests.</summary>
    public static ProviderRegistrationHandle UseMismatchedInMemory(
        this ProviderCollectionBuilder providers)
    {
        var registration = new ProviderRegistrationBuilder();
        registration.ConfigureProvider(_ => new InMemoryStorageProvider());
        return providers.Register(SFTestProvider.Mismatched, registration);
    }
}

/// <summary>
/// Configures provider-level policies for the in-memory test provider.
/// </summary>
public sealed class InMemoryRegistrationBuilder
{
    private readonly ProviderRegistrationBuilder _registration;

    internal InMemoryRegistrationBuilder(ProviderRegistrationBuilder registration)
    {
        _registration = registration;
    }

    /// <summary>Validation policy overrides.</summary>
    public ValidationPolicyBuilder Validation => _registration.Validation;

    /// <summary>Naming policy overrides.</summary>
    public NamingPolicyBuilder Naming => _registration.Naming;

    /// <summary>Presigned URL policy overrides.</summary>
    public PresignedUrlPolicyBuilder PresignedUrls => _registration.PresignedUrls;

    /// <summary>Delivery URL policy overrides.</summary>
    public DeliveryUrlPolicyBuilder DeliveryUrls => _registration.DeliveryUrls;
}
