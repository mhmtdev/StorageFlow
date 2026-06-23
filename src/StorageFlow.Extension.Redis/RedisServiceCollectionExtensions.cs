using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using StorageFlow.Abstractions.Interfaces;

namespace StorageFlow.Extension.Redis;

/// <summary>
/// Extension methods for adding Redis presigned URL caching to StorageFlow.
/// </summary>
public static class RedisServiceCollectionExtensions
{
    /// <summary>
    /// Adds Redis-backed presigned URL caching to the service collection.
    /// Uses <see cref="Microsoft.Extensions.Caching.StackExchangeRedis"/> under the hood
    /// but exposes the cache through the standard <c>IDistributedCache</c> abstraction.
    /// </summary>
    /// <param name="services">The service collection (returned from <c>AddStorageFlow()</c>).</param>
    /// <param name="configure">Delegate to configure Redis cache options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddStorageFlow(opts => { ... })
    ///         .UseRedisPresignedUrlCache(redis =>
    ///         {
    ///             redis.ConnectionString = "localhost:6379";
    ///             redis.KeyPrefix = "storageflow:presigned:";
    ///         });
    /// </code>
    /// </example>
    public static IServiceCollection UseRedisPresignedUrlCache(
        this IServiceCollection services,
        Action<RedisPresignedUrlCacheOptions> configure)
    {
        var options = new RedisPresignedUrlCacheOptions();
        configure(options);

        // Register Redis as IDistributedCache.
        // KeyPrefix is applied only in RedisPresignedUrlCache.BuildCacheKey — not via InstanceName,
        // which would double-prefix every key.
        services.AddStackExchangeRedisCache(redis =>
        {
            redis.Configuration = options.ConnectionString;
        });

        services.AddSingleton(options);
        services.AddSingleton<RedisPresignedUrlCache>();
        services.AddSingleton<IPresignedUrlCache>(sp => sp.GetRequiredService<RedisPresignedUrlCache>());

        return services;
    }

    /// <summary>
    /// Adds presigned URL caching using an already-registered <c>IDistributedCache</c>.
    /// Use this overload when you have already configured a cache (e.g. in-memory, SQL).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Delegate to configure cache key and TTL options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection UseDistributedPresignedUrlCache(
        this IServiceCollection services,
        Action<RedisPresignedUrlCacheOptions>? configure = null)
    {
        var options = new RedisPresignedUrlCacheOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<RedisPresignedUrlCache>();
        services.AddSingleton<IPresignedUrlCache>(sp => sp.GetRequiredService<RedisPresignedUrlCache>());

        return services;
    }
}

