using Microsoft.Extensions.DependencyInjection;
using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Extension.Redis;
using StorageFlow.Tests.Integration.Fixtures;
using StorageFlow.Tests.Integration.Infrastructure;

namespace StorageFlow.Tests.Integration.Cache;

[Collection(CollectionName)]
public sealed class RedisIntegrationTests(RedisFixture fixture)
{
    public const string CollectionName = "Redis";

    [DockerFact]
    public async Task SetGetRemove_RoundTripsThroughRedis()
    {
        await using var services = BuildServices();
        var cache = services.GetRequiredService<IPresignedUrlCache>();

        await cache.SetAsync(
            "minio",
            "media",
            "photo.jpg",
            "download",
            "https://storage.example/signed",
            TimeSpan.FromMinutes(5));
        var cached = await cache.GetAsync(
            "minio", "media", "photo.jpg", "download");
        await cache.RemoveAsync(
            "minio", "media", "photo.jpg", "download");
        var removed = await cache.GetAsync(
            "minio", "media", "photo.jpg", "download");

        Assert.Equal("https://storage.example/signed", cached);
        Assert.Null(removed);
    }

    [DockerFact]
    public async Task TtlAndPolicyKey_AreEnforcedByRedis()
    {
        await using var services = BuildServices();
        var cache = services.GetRequiredService<IPresignedUrlCache>();

        await cache.SetAsync(
            "s3", "media", "photo.jpg", "short",
            "https://storage.example/short", TimeSpan.FromMilliseconds(100));
        await cache.SetAsync(
            "s3", "media", "photo.jpg", "long",
            "https://storage.example/long", TimeSpan.FromMinutes(5));
        await Task.Delay(250);

        Assert.Null(await cache.GetAsync(
            "s3", "media", "photo.jpg", "short"));
        Assert.Equal(
            "https://storage.example/long",
            await cache.GetAsync("s3", "media", "photo.jpg", "long"));
    }

    private ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.UseRedisPresignedUrlCache(options =>
        {
            options.ConnectionString = fixture.ConnectionString;
            options.KeyPrefix = $"storageflow:integration:{Guid.NewGuid():N}:";
        });
        return services.BuildServiceProvider();
    }
}

[CollectionDefinition(RedisIntegrationTests.CollectionName)]
public sealed class RedisCollection : ICollectionFixture<RedisFixture>;
