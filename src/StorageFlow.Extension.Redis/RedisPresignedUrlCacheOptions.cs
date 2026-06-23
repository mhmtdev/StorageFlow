namespace StorageFlow.Extension.Redis;

/// <summary>
/// Configuration options for the Redis presigned URL cache.
/// </summary>
public sealed class RedisPresignedUrlCacheOptions
{
    /// <summary>
    /// Redis connection string (e.g. "localhost:6379" or "redis.example.com:6380,password=secret").
    /// Required when using <c>UseRedisPresignedUrlCache()</c> with an auto-configured connection.
    /// </summary>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Key prefix prepended to every cache key.
    /// Use this to namespace keys when multiple applications share the same Redis instance.
    /// Default: <c>"storageflow:presigned:"</c>.
    /// </summary>
    public string KeyPrefix { get; set; } = "storageflow:presigned:";

    /// <summary>
    /// Optional fixed TTL override. When set, cached URLs always expire after this duration
    /// regardless of the presigned URL policy expiration.
    /// Leave <c>null</c> to mirror the policy expiration (recommended).
    /// </summary>
    public TimeSpan? AbsoluteExpiration { get; set; }
}

