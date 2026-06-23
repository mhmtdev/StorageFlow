using StorageFlow.Abstractions.Configuration;
using StorageFlow.Abstractions.Exceptions;
using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Abstractions.Models;

namespace StorageFlow.Core.Configuration;

/// <summary>
/// Registers global naming policies and optionally selects one as the default.
/// </summary>
public sealed class GlobalNamingPolicyBuilder
{
    private readonly NamingPolicyBuilder _policies = new();
    private Type? _defaultPolicyKey;

    internal IReadOnlyDictionary<Type, NamingPolicy> Policies => _policies.Policies;

    internal Type? DefaultPolicyKey => _defaultPolicyKey;

    /// <summary>Adds or replaces a global naming policy.</summary>
    public GlobalNamingPolicyRegistrationHandle AddPolicy<TPolicyKey>(
        Action<NamingPolicy> configure)
        where TPolicyKey : INamingPolicyKey
    {
        _policies.AddPolicy<TPolicyKey>(configure);
        return new GlobalNamingPolicyRegistrationHandle(
            this,
            typeof(TPolicyKey));
    }

    internal void SetDefault(Type policyKey)
    {
        if (_defaultPolicyKey is not null)
        {
            throw new StorageConfigurationException(
                $"Default naming policy is already set to '{_defaultPolicyKey.FullName}'.");
        }

        _defaultPolicyKey = policyKey;
    }
}

/// <summary>
/// Represents a completed global naming policy registration.
/// </summary>
public sealed class GlobalNamingPolicyRegistrationHandle
{
    private readonly GlobalNamingPolicyBuilder _builder;
    private readonly Type _policyKey;

    internal GlobalNamingPolicyRegistrationHandle(
        GlobalNamingPolicyBuilder builder,
        Type policyKey)
    {
        _builder = builder;
        _policyKey = policyKey;
    }

    /// <summary>Marks this naming policy as the global default.</summary>
    public GlobalNamingPolicyRegistrationHandle AsDefault()
    {
        _builder.SetDefault(_policyKey);
        return this;
    }
}
