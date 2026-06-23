using StorageFlow.Abstractions.Models;

namespace StorageFlow.Abstractions.Interfaces;

/// <summary>
/// Resolves the naming strategy configured by a naming policy.
/// </summary>
public interface INamingStrategyResolver
{
    /// <summary>Resolves the strategy represented by <paramref name="policy"/>.</summary>
    IFileNamingStrategy Resolve(NamingPolicy policy);
}
