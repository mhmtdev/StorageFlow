using StorageFlow.Abstractions.Interfaces;

namespace StorageFlow.Abstractions.Models;

/// <summary>
/// Defines how object keys are generated for an upload.
/// </summary>
public sealed class NamingPolicy
{
    /// <summary>The configured strategy kind.</summary>
    public NamingStrategyKind StrategyKind { get; private set; } = NamingStrategyKind.Guid;

    /// <summary>The custom strategy type when <see cref="StrategyKind"/> is custom.</summary>
    public Type? StrategyType { get; private set; }

    /// <summary>The default pattern used by pattern-based naming.</summary>
    public string? Pattern { get; private set; }

    /// <summary>Uses GUID-based object keys.</summary>
    public NamingPolicy UseGuid()
    {
        StrategyKind = NamingStrategyKind.Guid;
        StrategyType = null;
        Pattern = null;
        return this;
    }

    /// <summary>Uses SEO-friendly object keys.</summary>
    public NamingPolicy UseSeo()
    {
        StrategyKind = NamingStrategyKind.Seo;
        StrategyType = null;
        Pattern = null;
        return this;
    }

    /// <summary>Uses pattern-based object keys with the supplied default pattern.</summary>
    public NamingPolicy UsePattern(string defaultPattern)
    {
        StrategyKind = NamingStrategyKind.Pattern;
        StrategyType = null;
        Pattern = defaultPattern;
        return this;
    }

    /// <summary>Uses an application-defined naming strategy resolved from dependency injection.</summary>
    public NamingPolicy UseStrategy<TStrategy>()
        where TStrategy : class, IFileNamingStrategy
    {
        StrategyKind = NamingStrategyKind.Custom;
        StrategyType = typeof(TStrategy);
        Pattern = null;
        return this;
    }
}

/// <summary>
/// Identifies the strategy represented by a naming policy.
/// </summary>
public enum NamingStrategyKind
{
    /// <summary>GUID-based naming.</summary>
    Guid,

    /// <summary>SEO-friendly naming.</summary>
    Seo,

    /// <summary>Pattern-based naming.</summary>
    Pattern,

    /// <summary>An application-defined strategy resolved from dependency injection.</summary>
    Custom
}
