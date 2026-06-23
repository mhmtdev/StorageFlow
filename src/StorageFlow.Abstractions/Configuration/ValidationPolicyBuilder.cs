using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Abstractions.Models;

namespace StorageFlow.Abstractions.Configuration;

/// <summary>
/// Registers validation policies using strongly-typed policy keys.
/// </summary>
public sealed class ValidationPolicyBuilder
{
    private readonly Dictionary<Type, ValidationPolicy> _policies = [];

    /// <summary>Registered policies keyed by their marker type.</summary>
    public IReadOnlyDictionary<Type, ValidationPolicy> Policies => _policies;

    /// <summary>Adds or replaces a validation policy.</summary>
    public ValidationPolicyBuilder AddPolicy<TPolicyKey>(
        Action<ValidationPolicy> configure)
        where TPolicyKey : IValidationPolicyKey
    {
        var policy = new ValidationPolicy();
        configure(policy);
        _policies[typeof(TPolicyKey)] = policy;
        return this;
    }
}
