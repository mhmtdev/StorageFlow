using StorageFlow.Abstractions.Models;

namespace StorageFlow.Core.Configuration;

/// <summary>
/// Holds provider-level policy overrides.
/// These take precedence over global policies when the same policy key type is requested.
/// </summary>
public sealed class ProviderPolicyOverrides
{
    private readonly Dictionary<Type, ValidationPolicy> _validationPolicies;
    private readonly Dictionary<Type, NamingPolicy> _namingPolicies;
    private readonly Dictionary<Type, PresignedUrlPolicy> _presignedUrlPolicies;
    private readonly Dictionary<Type, DeliveryUrlPolicy> _deliveryUrlPolicies;

    /// <summary>Creates an empty override collection.</summary>
    public ProviderPolicyOverrides()
        : this(
            new Dictionary<Type, ValidationPolicy>(),
            new Dictionary<Type, NamingPolicy>(),
            new Dictionary<Type, PresignedUrlPolicy>(),
            new Dictionary<Type, DeliveryUrlPolicy>())
    {
    }

    /// <summary>Creates overrides from provider registration builders.</summary>
    public ProviderPolicyOverrides(
        IReadOnlyDictionary<Type, ValidationPolicy> validationPolicies,
        IReadOnlyDictionary<Type, NamingPolicy> namingPolicies,
        IReadOnlyDictionary<Type, PresignedUrlPolicy> presignedUrlPolicies,
        IReadOnlyDictionary<Type, DeliveryUrlPolicy> deliveryUrlPolicies)
    {
        _validationPolicies = new Dictionary<Type, ValidationPolicy>(validationPolicies);
        _namingPolicies = new Dictionary<Type, NamingPolicy>(namingPolicies);
        _presignedUrlPolicies = new Dictionary<Type, PresignedUrlPolicy>(presignedUrlPolicies);
        _deliveryUrlPolicies = new Dictionary<Type, DeliveryUrlPolicy>(deliveryUrlPolicies);
    }

    /// <summary>All validation policy overrides keyed by policy type.</summary>
    public IReadOnlyDictionary<Type, ValidationPolicy> ValidationPolicies => _validationPolicies;

    /// <summary>All naming policy overrides keyed by policy type.</summary>
    public IReadOnlyDictionary<Type, NamingPolicy> NamingPolicies => _namingPolicies;

    /// <summary>All presigned URL policy overrides keyed by policy type.</summary>
    public IReadOnlyDictionary<Type, PresignedUrlPolicy> PresignedUrlPolicies => _presignedUrlPolicies;

    /// <summary>All delivery URL policy overrides keyed by policy type.</summary>
    public IReadOnlyDictionary<Type, DeliveryUrlPolicy> DeliveryUrlPolicies => _deliveryUrlPolicies;

    /// <summary>Adds or replaces a validation policy override.</summary>
    public ProviderPolicyOverrides AddValidationPolicy<TPolicyKey>(
        Action<ValidationPolicy> configure)
        where TPolicyKey : StorageFlow.Abstractions.Interfaces.IValidationPolicyKey
    {
        var policy = new ValidationPolicy();
        configure(policy);
        _validationPolicies[typeof(TPolicyKey)] = policy;
        return this;
    }

    /// <summary>Adds or replaces a presigned URL policy override.</summary>
    public ProviderPolicyOverrides AddPresignedUrlPolicy<TPolicyKey>(
        Action<PresignedUrlPolicy> configure)
        where TPolicyKey : StorageFlow.Abstractions.Interfaces.IPresignedUrlPolicyKey
    {
        var policy = new PresignedUrlPolicy();
        configure(policy);
        _presignedUrlPolicies[typeof(TPolicyKey)] = policy;
        return this;
    }

    /// <summary>Adds or replaces a naming policy override.</summary>
    public ProviderPolicyOverrides AddNamingPolicy<TPolicyKey>(
        Action<NamingPolicy> configure)
        where TPolicyKey : StorageFlow.Abstractions.Interfaces.INamingPolicyKey
    {
        var policy = new NamingPolicy();
        configure(policy);
        _namingPolicies[typeof(TPolicyKey)] = policy;
        return this;
    }

    /// <summary>Adds or replaces a delivery URL policy override.</summary>
    public ProviderPolicyOverrides AddDeliveryUrlPolicy<TPolicyKey>(
        Action<DeliveryUrlPolicy> configure)
        where TPolicyKey : StorageFlow.Abstractions.Interfaces.IDeliveryUrlPolicyKey
    {
        var policy = new DeliveryUrlPolicy();
        configure(policy);
        policy.ValidateAndNormalize();
        _deliveryUrlPolicies[typeof(TPolicyKey)] = policy;
        return this;
    }
}
