using StorageFlow.Abstractions.Exceptions;
using StorageFlow.Abstractions.Interfaces;

namespace StorageFlow.Core.Configuration;

/// <summary>
/// Registers strongly-typed storage profiles.
/// </summary>
public sealed class StorageProfileCollectionBuilder
{
    private readonly Dictionary<Type, StorageProfile> _profiles = [];

    internal IReadOnlyDictionary<Type, StorageProfile> Profiles => _profiles;

    /// <summary>Adds a storage profile using a marker key type.</summary>
    public StorageProfileCollectionBuilder Add<TProfileKey>(
        Action<StorageProfile> configure)
        where TProfileKey : IStorageProfileKey
    {
        var key = typeof(TProfileKey);
        if (_profiles.ContainsKey(key))
        {
            throw new StorageConfigurationException(
                $"Storage profile '{key.FullName}' is already registered.");
        }

        var profile = new StorageProfile();
        configure(profile);
        _profiles.Add(key, profile);
        return this;
    }
}
