namespace StorageFlow.Abstractions.Models;

/// <summary>
/// Identifies a first-party StorageFlow provider.
/// Tokens are created only by StorageFlow packages.
/// </summary>
public sealed class StorageProviderToken : IEquatable<StorageProviderToken>
{
    internal StorageProviderToken(string name)
    {
        Name = name;
    }

    internal string Name { get; }

    /// <inheritdoc />
    public bool Equals(StorageProviderToken? other) =>
        other is not null &&
        StringComparer.OrdinalIgnoreCase.Equals(Name, other.Name);

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is StorageProviderToken other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(Name);

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>
/// Catalog of first-party StorageFlow providers.
/// </summary>
public static class SFProvider
{
    /// <summary>MinIO provider token.</summary>
    public static StorageProviderToken Minio { get; } = new("minio");

    /// <summary>AWS S3 provider token.</summary>
    public static StorageProviderToken S3 { get; } = new("s3");

    /// <summary>RustFS provider token.</summary>
    public static StorageProviderToken RustFs { get; } = new("rustfs");
}
