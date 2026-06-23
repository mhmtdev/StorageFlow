using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Extension.Redis;

namespace StorageFlow.Tests.Unit.Cache;

public class RedisServiceCollectionExtensionsTests
{
    [Fact]
    public void UseDistributedPresignedUrlCache_RegistersCacheServices()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();

        services.UseDistributedPresignedUrlCache(opts =>
        {
            opts.KeyPrefix = "test:";
        });

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<RedisPresignedUrlCacheOptions>());
        Assert.NotNull(provider.GetService<RedisPresignedUrlCache>());
        Assert.NotNull(provider.GetService<IPresignedUrlCache>());
        Assert.Same(
            provider.GetRequiredService<RedisPresignedUrlCache>(),
            provider.GetRequiredService<IPresignedUrlCache>());
    }

    [Fact]
    public async Task UseDistributedPresignedUrlCache_ResolvesWorkingCache()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        services.UseDistributedPresignedUrlCache(opts => opts.KeyPrefix = "sf:");

        await using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IPresignedUrlCache>();

        await cache.SetAsync("minio", "media", "photo.jpg", "download",
            "https://example.com/signed", TimeSpan.FromMinutes(30));

        var url = await cache.GetAsync("minio", "media", "photo.jpg", "download");

        Assert.Equal("https://example.com/signed", url);
    }

    [Fact]
    public void UseRedisPresignedUrlCache_RegistersCacheServices()
    {
        var services = new ServiceCollection();

        services.UseRedisPresignedUrlCache(opts =>
        {
            opts.ConnectionString = "localhost:6379";
            opts.KeyPrefix = "storageflow:presigned:";
        });

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<RedisPresignedUrlCacheOptions>());
        Assert.NotNull(provider.GetService<RedisPresignedUrlCache>());
        Assert.NotNull(provider.GetService<IPresignedUrlCache>());
        Assert.NotNull(provider.GetService<IDistributedCache>());
    }
}
