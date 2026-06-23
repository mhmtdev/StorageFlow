using StorageFlow.Abstractions.Configuration;
using StorageFlow.Abstractions.Models;

namespace StorageFlow.Core.Configuration;

/// <summary>
/// Root configuration object for StorageFlow.
/// Passed to the <c>AddStorageFlow(options => ...)</c> callback.
/// </summary>
public sealed class StorageFlowOptions
{
    internal Dictionary<Type, ValidationPolicy> GlobalValidationPolicies { get; } = [];
    internal Dictionary<Type, NamingPolicy> GlobalNamingPolicies { get; } = [];
    internal Dictionary<Type, PresignedUrlPolicy> GlobalPresignedUrlPolicies { get; } = [];
    internal Dictionary<Type, DeliveryUrlPolicy> GlobalDeliveryUrlPolicies { get; } = [];
    internal Type? DefaultNamingPolicyKey { get; private set; }

    /// <summary>Provider registration builder extended by installed provider packages.</summary>
    public ProviderCollectionBuilder Providers { get; } = new();

    /// <summary>Global validation policy builder. Use <c>options.Validation.AddPolicy(...)</c>.</summary>
    public ValidationPolicyBuilder Validation { get; } = new();

    /// <summary>Global naming policy builder. Use <c>options.Naming.AddPolicy(...)</c>.</summary>
    public GlobalNamingPolicyBuilder Naming { get; } = new();

    /// <summary>Global presigned URL policy builder. Use <c>options.PresignedUrls.AddPolicy(...)</c>.</summary>
    public PresignedUrlPolicyBuilder PresignedUrls { get; } = new();

    /// <summary>Global delivery URL policy builder.</summary>
    public DeliveryUrlPolicyBuilder DeliveryUrls { get; } = new();

    /// <summary>Strongly-typed storage profile registrations.</summary>
    public StorageProfileCollectionBuilder Profiles { get; } = new();

    internal IReadOnlyDictionary<Type, StorageProfile> RegisteredProfiles =>
        Profiles.Profiles;

    // ── Internal flush ───────────────────────────────────────────────────────

    /// <summary>
    /// Applies global policy builders to the internal dictionaries and auto-sets the default provider
    /// when only one provider is registered. Called automatically by <c>AddStorageFlow()</c>.
    /// </summary>
    public void Flush()
    {
        foreach (var (key, policy) in Validation.Policies)
            GlobalValidationPolicies[key] = policy;

        foreach (var (key, policy) in Naming.Policies)
            GlobalNamingPolicies[key] = policy;

        DefaultNamingPolicyKey = Naming.DefaultPolicyKey;

        foreach (var (key, policy) in PresignedUrls.Policies)
            GlobalPresignedUrlPolicies[key] = policy;

        foreach (var (key, policy) in DeliveryUrls.Policies)
            GlobalDeliveryUrlPolicies[key] = policy;

        Providers.ApplyAutomaticDefault();
    }
}
