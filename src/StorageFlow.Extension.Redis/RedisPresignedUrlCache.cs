using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using StorageFlow.Abstractions.Interfaces;

namespace StorageFlow.Extension.Redis;

/// <summary>
/// Presigned URL cache backed by <see cref="IDistributedCache"/>.
/// Works with any compatible implementation: Redis, in-memory, SQL Server, etc.
/// </summary>
/// <remarks>
/// Cache key format: <c>{prefix}{providerName}:{bucket}:{objectKey}:{policyKey}</c>
/// TTL mirrors the presigned URL policy expiration so the cached URL never outlives the URL itself.
/// </remarks>
public sealed class RedisPresignedUrlCache : IPresignedUrlCache
{
    private readonly IDistributedCache _cache;
    private readonly RedisPresignedUrlCacheOptions _options;

    /// <param name="cache">The distributed cache implementation.</param>
    /// <param name="options">Cache configuration options.</param>
    public RedisPresignedUrlCache(IDistributedCache cache, RedisPresignedUrlCacheOptions options)
    {
        _cache = cache;
        _options = options;
    }

    /// <summary>
    /// Attempts to retrieve a cached presigned URL.
    /// Returns <c>null</c> when the entry does not exist or has expired.
    /// </summary>
    public async Task<string?> GetAsync(
        string providerName,
        string bucket,
        string objectKey,
        string policyKey,
        CancellationToken cancellationToken = default)
    {
        var key = BuildCacheKey(providerName, bucket, objectKey, policyKey);
        var bytes = await _cache.GetAsync(key, cancellationToken);
        return bytes is null ? null : Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Stores a presigned URL in the cache with an expiration matching the policy TTL.
    /// </summary>
    /// <param name="providerName">The provider that generated the URL.</param>
    /// <param name="bucket">The bucket the URL refers to.</param>
    /// <param name="objectKey">The object key the URL refers to.</param>
    /// <param name="policyKey">The internal identifier derived from the policy key type.</param>
    /// <param name="url">The presigned URL to cache.</param>
    /// <param name="policyExpiration">
    /// The expiration duration from the presigned URL policy.
    /// The cache entry will expire at the same time as the URL.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SetAsync(
        string providerName,
        string bucket,
        string objectKey,
        string policyKey,
        string url,
        TimeSpan policyExpiration,
        CancellationToken cancellationToken = default)
    {
        var key = BuildCacheKey(providerName, bucket, objectKey, policyKey);
        var ttl = _options.AbsoluteExpiration ?? policyExpiration;

        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl
        };

        await _cache.SetAsync(key, Encoding.UTF8.GetBytes(url), cacheOptions, cancellationToken);
    }

    /// <summary>
    /// Removes a cached presigned URL entry.
    /// </summary>
    public async Task RemoveAsync(
        string providerName,
        string bucket,
        string objectKey,
        string policyKey,
        CancellationToken cancellationToken = default)
    {
        var key = BuildCacheKey(providerName, bucket, objectKey, policyKey);
        await _cache.RemoveAsync(key, cancellationToken);
    }

    // ── Key builder ───────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a cache key in the format:
    /// <c>{prefix}{providerName}:{bucket}:{objectKey}:{policyKey}</c>
    /// </summary>
    public string BuildCacheKey(string providerName, string bucket, string objectKey, string policyKey)
        => $"{_options.KeyPrefix}{providerName.ToLowerInvariant()}:{bucket}:{objectKey}:{policyKey}";
}

