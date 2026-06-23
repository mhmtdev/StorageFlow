using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Abstractions.Models;

namespace StorageFlow.Core.Configuration;

/// <summary>
/// Groups provider, bucket, validation, naming, and presigned URL settings.
/// </summary>
public sealed class StorageProfile
{
    internal StorageProviderToken? ProviderToken { get; private set; }
    internal string? BucketName { get; private set; }
    internal Type? ValidationPolicyKey { get; private set; }
    internal Type? NamingPolicyKey { get; private set; }
    internal Type? PresignedUrlPolicyKey { get; private set; }

    /// <summary>Selects the provider used by this profile.</summary>
    public StorageProfile Provider(StorageProviderToken provider)
    {
        ProviderToken = provider;
        return this;
    }

    /// <summary>Sets the default bucket used by this profile.</summary>
    public StorageProfile Bucket(string bucket)
    {
        BucketName = bucket;
        return this;
    }

    /// <summary>Selects the validation policy used by this profile.</summary>
    public StorageProfile Validation<TPolicyKey>()
        where TPolicyKey : IValidationPolicyKey
    {
        ValidationPolicyKey = typeof(TPolicyKey);
        return this;
    }

    /// <summary>Selects the naming policy used by this profile.</summary>
    public StorageProfile Naming<TPolicyKey>()
        where TPolicyKey : INamingPolicyKey
    {
        NamingPolicyKey = typeof(TPolicyKey);
        return this;
    }

    /// <summary>Selects the presigned URL policy used by this profile.</summary>
    public StorageProfile PresignedUrl<TPolicyKey>()
        where TPolicyKey : IPresignedUrlPolicyKey
    {
        PresignedUrlPolicyKey = typeof(TPolicyKey);
        return this;
    }
}
