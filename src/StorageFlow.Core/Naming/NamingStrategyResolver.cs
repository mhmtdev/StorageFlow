using Microsoft.Extensions.DependencyInjection;
using StorageFlow.Abstractions.Exceptions;
using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Abstractions.Models;

namespace StorageFlow.Core.Naming;

/// <summary>
/// Resolves built-in and application-defined naming strategies from dependency injection.
/// </summary>
public sealed class NamingStrategyResolver : INamingStrategyResolver
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>Creates a resolver backed by <paramref name="serviceProvider"/>.</summary>
    public NamingStrategyResolver(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public IFileNamingStrategy Resolve(NamingPolicy policy) =>
        policy.StrategyKind switch
        {
            NamingStrategyKind.Guid => _serviceProvider.GetRequiredService<GuidNamingStrategy>(),
            NamingStrategyKind.Seo => _serviceProvider.GetRequiredService<SeoNamingStrategy>(),
            NamingStrategyKind.Pattern => _serviceProvider.GetRequiredService<PatternNamingStrategy>(),
            NamingStrategyKind.Custom => ResolveCustomStrategy(policy),
            _ => throw new StorageConfigurationException(
                $"Unsupported naming strategy kind '{policy.StrategyKind}'.")
        };

    private IFileNamingStrategy ResolveCustomStrategy(NamingPolicy policy)
    {
        if (policy.StrategyType is null)
            throw new StorageConfigurationException(
                "A custom naming policy must specify a strategy type.");

        if (_serviceProvider.GetService(policy.StrategyType) is IFileNamingStrategy strategy)
            return strategy;

        throw new StorageConfigurationException(
            $"Naming strategy '{policy.StrategyType.FullName}' is not registered. " +
            $"Call AddStorageFlowNamingStrategy<{policy.StrategyType.Name}>().");
    }
}
