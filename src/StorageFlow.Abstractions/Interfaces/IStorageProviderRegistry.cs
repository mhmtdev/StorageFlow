using StorageFlow.Abstractions.Models;

namespace StorageFlow.Abstractions.Interfaces;

/// <summary>
/// Internal registry that holds all registered providers and resolves them by token.
/// Application code should not depend on this interface directly — use <see cref="IStorageService"/> instead.
/// </summary>
public interface IStorageProviderRegistry
{
    /// <summary>
    /// Resolves a provider using a first-party provider token.
    /// </summary>
    /// <param name="provider">The provider token.</param>
    /// <returns>The matching <see cref="IStorageProvider"/>.</returns>
    /// <exception cref="Exceptions.StorageConfigurationException">
    /// Thrown when no provider is registered for the supplied token.
    /// </exception>
    IStorageProvider Get(StorageProviderToken provider);

    /// <summary>
    /// Returns the default provider.
    /// If only one provider is registered it is automatically the default.
    /// </summary>
    /// <exception cref="Exceptions.StorageConfigurationException">
    /// Thrown when multiple providers are registered and no default has been set.
    /// </exception>
    IStorageProvider GetDefault();

    /// <summary>Returns the token of the default provider.</summary>
    StorageProviderToken GetDefaultProviderToken();

    /// <summary>
    /// Returns all registered provider tokens.
    /// </summary>
    IEnumerable<StorageProviderToken> GetRegisteredProviders();
}
