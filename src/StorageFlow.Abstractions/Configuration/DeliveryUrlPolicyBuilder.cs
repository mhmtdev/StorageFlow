using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Abstractions.Models;

namespace StorageFlow.Abstractions.Configuration;

/// <summary>
/// Registers typed CDN delivery URL policies.
/// </summary>
public class DeliveryUrlPolicyBuilder
{
    private readonly Dictionary<Type, DeliveryUrlPolicy> _policies = [];

    /// <summary>Registered policies keyed by marker type.</summary>
    public IReadOnlyDictionary<Type, DeliveryUrlPolicy> Policies => _policies;

    /// <summary>Adds or replaces a delivery URL policy.</summary>
    public DeliveryUrlPolicyBuilder AddPolicy<TPolicyKey>(
        Action<DeliveryUrlPolicy> configure)
        where TPolicyKey : IDeliveryUrlPolicyKey
    {
        var policy = new DeliveryUrlPolicy();
        configure(policy);
        policy.ValidateAndNormalize();
        _policies[typeof(TPolicyKey)] = policy;
        return this;
    }
}
