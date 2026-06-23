using StorageFlow.Abstractions.Exceptions;
using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Abstractions.Models;

namespace StorageFlow.Abstractions.Configuration;

/// <summary>
/// Fluent entry point extended by first-party provider packages.
/// </summary>
public sealed class ProviderCollectionBuilder
{
    private readonly Dictionary<StorageProviderToken, ProviderRegistrationDescriptor> _registrations = [];
    private StorageProviderToken? _defaultProvider;
    private bool _defaultWasExplicitlySet;

    internal IReadOnlyDictionary<StorageProviderToken, ProviderRegistrationDescriptor> Registrations =>
        _registrations;

    internal StorageProviderToken? DefaultProvider => _defaultProvider;

    internal ProviderRegistrationHandle Register(
        StorageProviderToken token,
        ProviderRegistrationBuilder registration)
    {
        registration.ValidateConfigured(token);

        if (!_registrations.TryAdd(token, new ProviderRegistrationDescriptor(
                token,
                registration.ProviderFactory!,
                registration.Validation.Policies,
                registration.Naming.Policies,
                registration.PresignedUrls.Policies,
                registration.DeliveryUrls.Policies)))
        {
            throw new StorageConfigurationException(
                $"Provider '{token}' is already registered.");
        }

        return new ProviderRegistrationHandle(this, token);
    }

    internal void SetDefault(StorageProviderToken token)
    {
        if (_defaultWasExplicitlySet)
            throw new StorageConfigurationException(
                $"Default provider is already set to '{_defaultProvider}'.");

        if (!_registrations.ContainsKey(token))
            throw new StorageConfigurationException(
                $"Provider '{token}' must be registered before it can be selected as default.");

        _defaultProvider = token;
        _defaultWasExplicitlySet = true;
    }

    internal void ApplyAutomaticDefault()
    {
        if (_defaultProvider is null && _registrations.Count == 1)
            _defaultProvider = _registrations.Keys.Single();
    }
}

/// <summary>
/// Represents a completed provider registration.
/// </summary>
public sealed class ProviderRegistrationHandle
{
    private readonly ProviderCollectionBuilder _providers;
    private readonly StorageProviderToken _token;

    internal ProviderRegistrationHandle(
        ProviderCollectionBuilder providers,
        StorageProviderToken token)
    {
        _providers = providers;
        _token = token;
    }

    /// <summary>Marks this provider as the default provider.</summary>
    public ProviderRegistrationHandle AsDefault()
    {
        _providers.SetDefault(_token);
        return this;
    }
}

internal sealed record ProviderRegistrationDescriptor(
    StorageProviderToken Token,
    Func<IServiceProvider, IStorageProvider> Factory,
    IReadOnlyDictionary<Type, ValidationPolicy> ValidationPolicies,
    IReadOnlyDictionary<Type, NamingPolicy> NamingPolicies,
    IReadOnlyDictionary<Type, PresignedUrlPolicy> PresignedUrlPolicies,
    IReadOnlyDictionary<Type, DeliveryUrlPolicy> DeliveryUrlPolicies);
