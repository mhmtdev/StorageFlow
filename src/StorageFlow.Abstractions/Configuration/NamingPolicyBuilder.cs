using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Abstractions.Models;

namespace StorageFlow.Abstractions.Configuration;

/// <summary>
/// Registers naming policies using strongly-typed policy keys.
/// </summary>
public sealed class NamingPolicyBuilder
{
    private readonly Dictionary<Type, NamingPolicy> _policies = [];

    /// <summary>Registered policies keyed by their marker type.</summary>
    public IReadOnlyDictionary<Type, NamingPolicy> Policies => _policies;

    /// <summary>Adds or replaces a naming policy.</summary>
    public NamingPolicyBuilder AddPolicy<TPolicyKey>(
        Action<NamingPolicy> configure)
        where TPolicyKey : INamingPolicyKey
    {
        var policy = new NamingPolicy();
        configure(policy);
        _policies[typeof(TPolicyKey)] = policy;
        return this;
    }
}
