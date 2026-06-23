using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StorageFlow.Extension.Redis;

namespace StorageFlow.Tests.Unit.Cache;

/// <summary>
/// Tests for <see cref="RedisPresignedUrlCache"/> using an in-memory IDistributedCache
/// — no real Redis instance required.
/// </summary>
public class RedisPresignedUrlCacheTests
{
    private static IDistributedCache InMemoryCache()
    {
        var opts = Options.Create(new MemoryDistributedCacheOptions());
        return new MemoryDistributedCache(opts);
    }

    private static RedisPresignedUrlCache BuildCache(string prefix = "sf:") =>
        new(InMemoryCache(), new RedisPresignedUrlCacheOptions { KeyPrefix = prefix });

    // ── Get / Set roundtrip ───────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_WhenKeyNotCached_ReturnsNull()
    {
        var cache = BuildCache();

        var result = await cache.GetAsync("minio", "media", "photo.jpg", "download");

        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_ReturnsStoredUrl()
    {
        var cache = BuildCache();
        const string url = "https://minio.example.com/media/photo.jpg?sig=abc";

        await cache.SetAsync("minio", "media", "photo.jpg", "download", url, TimeSpan.FromMinutes(30));
        var result = await cache.GetAsync("minio", "media", "photo.jpg", "download");

        Assert.Equal(url, result);
    }

    [Fact]
    public async Task GetAsync_AfterRemove_ReturnsNull()
    {
        var cache = BuildCache();
        const string url = "https://example.com/signed";

        await cache.SetAsync("minio", "bucket", "key.jpg", "download", url, TimeSpan.FromMinutes(30));
        await cache.RemoveAsync("minio", "bucket", "key.jpg", "download");

        var result = await cache.GetAsync("minio", "bucket", "key.jpg", "download");

        Assert.Null(result);
    }

    // ── Cache key format ──────────────────────────────────────────────────────

    [Fact]
    public void BuildCacheKey_FormatsCorrectly()
    {
        var cache = BuildCache("storageflow:presigned:");

        var key = cache.BuildCacheKey("minio", "media", "2026/06/photo.jpg", "download");

        Assert.Equal("storageflow:presigned:minio:media:2026/06/photo.jpg:download", key);
    }

    [Fact]
    public void BuildCacheKey_ProviderNameIsLowercased()
    {
        var cache = BuildCache("sf:");

        var key = cache.BuildCacheKey("MinIO", "bucket", "obj.jpg", "download");

        Assert.StartsWith("sf:minio:", key);
    }

    [Fact]
    public void BuildCacheKey_DifferentProvidersSameBucketAndKey_ProduceDifferentKeys()
    {
        var cache = BuildCache("sf:");

        var keyMinio = cache.BuildCacheKey("minio", "media", "photo.jpg", "download");
        var keyS3    = cache.BuildCacheKey("s3",    "media", "photo.jpg", "download");

        Assert.NotEqual(keyMinio, keyS3);
    }

    [Fact]
    public void BuildCacheKey_DifferentPoliciesSameObject_ProduceDifferentKeys()
    {
        var cache = BuildCache("sf:");

        var downloadKey = cache.BuildCacheKey("minio", "media", "photo.jpg", "download");
        var uploadKey   = cache.BuildCacheKey("minio", "media", "photo.jpg", "upload");

        Assert.NotEqual(downloadKey, uploadKey);
    }

    // ── TTL mirrors policy expiration ─────────────────────────────────────────

    [Fact]
    public async Task SetAsync_WithShortTtl_ExpiresAsExpected()
    {
        var cache = new RedisPresignedUrlCache(
            InMemoryCache(),
            new RedisPresignedUrlCacheOptions { KeyPrefix = "sf:" });

        // Store with 1ms TTL so it expires immediately
        await cache.SetAsync("minio", "bucket", "key.jpg", "download",
            "https://expired.url", TimeSpan.FromMilliseconds(1));

        await Task.Delay(50); // wait for expiry

        var result = await cache.GetAsync("minio", "bucket", "key.jpg", "download");
        Assert.Null(result); // should have expired
    }

    [Fact]
    public async Task SetAsync_WithAbsoluteExpirationOverride_UsesOverride()
    {
        var options = new RedisPresignedUrlCacheOptions
        {
            KeyPrefix = "sf:",
            AbsoluteExpiration = TimeSpan.FromMilliseconds(1) // override: 1ms
        };
        var cache = new RedisPresignedUrlCache(InMemoryCache(), options);

        // Policy says 30 min, but override says 1ms
        await cache.SetAsync("minio", "bucket", "key.jpg", "download",
            "https://url", TimeSpan.FromMinutes(30));

        await Task.Delay(50);

        var result = await cache.GetAsync("minio", "bucket", "key.jpg", "download");
        Assert.Null(result); // expired due to override
    }

    // ── Options defaults ──────────────────────────────────────────────────────

    [Fact]
    public void DefaultOptions_HasCorrectDefaults()
    {
        var opts = new RedisPresignedUrlCacheOptions();

        Assert.Equal("storageflow:presigned:", opts.KeyPrefix);
        Assert.Equal("localhost:6379", opts.ConnectionString);
        Assert.Null(opts.AbsoluteExpiration);
    }
}

