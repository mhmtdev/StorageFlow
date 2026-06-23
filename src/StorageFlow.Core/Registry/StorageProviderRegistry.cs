using StorageFlow.Abstractions.Exceptions;
using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Abstractions.Models;

namespace StorageFlow.Core.Registry;

/// <summary>
/// Default implementation of <see cref="IStorageProviderRegistry"/>.
/// Holds all registered providers and resolves them by token.
/// </summary>
public sealed class StorageProviderRegistry : IStorageProviderRegistry
{
    private readonly IReadOnlyDictionary<StorageProviderToken, IStorageProvider> _providers;
    private readonly StorageProviderToken? _defaultProvider;

    /// <param name="providers">All registered providers keyed by token.</param>
    /// <param name="defaultProvider">The default provider token.</param>
    public StorageProviderRegistry(
        IReadOnlyDictionary<StorageProviderToken, IStorageProvider> providers,
        StorageProviderToken? defaultProvider)
    {
        _providers = providers;
        _defaultProvider = defaultProvider;
    }

    /// <inheritdoc />
    public IStorageProvider Get(StorageProviderToken provider)
    {
        if (_providers.TryGetValue(provider, out var implementation))
            return implementation;

        throw new StorageConfigurationException(
            $"Provider '{provider}' is not registered. " +
            $"Registered providers: {string.Join(", ", _providers.Keys)}.");
    }

    /// <inheritdoc />
    public IStorageProvider GetDefault()
    {
        return Get(GetDefaultProviderToken());
    }

    /// <inheritdoc />
    public StorageProviderToken GetDefaultProviderToken()
    {
        if (_defaultProvider is not null)
            return _defaultProvider;

        if (_providers.Count == 1)
            return _providers.Keys.First();

        throw new StorageConfigurationException(
            "Multiple providers are registered but no default provider has been set. " +
            "Call AsDefault() on one provider registration or use Provider(SFProvider.*).");
    }

    /// <inheritdoc />
    public IEnumerable<StorageProviderToken> GetRegisteredProviders() => _providers.Keys;
}
