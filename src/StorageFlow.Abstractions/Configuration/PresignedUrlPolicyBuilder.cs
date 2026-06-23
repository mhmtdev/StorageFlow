using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Abstractions.Models;

namespace StorageFlow.Abstractions.Configuration;

/// <summary>
/// Registers presigned URL policies using strongly-typed policy keys.
/// </summary>
public sealed class PresignedUrlPolicyBuilder
{
    private readonly Dictionary<Type, PresignedUrlPolicy> _policies = [];

    /// <summary>Registered policies keyed by their marker type.</summary>
    public IReadOnlyDictionary<Type, PresignedUrlPolicy> Policies => _policies;

    /// <summary>Adds or replaces a presigned URL policy.</summary>
    public PresignedUrlPolicyBuilder AddPolicy<TPolicyKey>(
        Action<PresignedUrlPolicy> configure)
        where TPolicyKey : IPresignedUrlPolicyKey
    {
        var policy = new PresignedUrlPolicy();
        configure(policy);
        _policies[typeof(TPolicyKey)] = policy;
        return this;
    }
}
