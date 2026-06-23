using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Core.DependencyInjection;
using StorageFlow.Extension.Redis;
using StorageFlow.Testing;

namespace StorageFlow.Tests.Component.Cache;

public class StorageServiceCacheTests
{
    private sealed class DownloadPolicy : IPresignedUrlPolicyKey;

    private static IDistributedCache InMemoryDistributedCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private static (IStorageService Service, IPresignedUrlCache Cache) BuildService()
    {
        var services = new ServiceCollection();
        services.AddSingleton(InMemoryDistributedCache());
        services.UseDistributedPresignedUrlCache(options => options.KeyPrefix = "sf:");
        services.AddStorageFlow(options =>
        {
            options.Providers.UseInMemory();
            options.PresignedUrls.AddPolicy<DownloadPolicy>(policy =>
            {
                policy.Expiration = TimeSpan.FromMinutes(30);
                policy.HttpMethod = HttpMethod.Get;
            });
        });

        var provider = services.BuildServiceProvider();
        return (
            provider.GetRequiredService<IStorageService>(),
            provider.GetRequiredService<IPresignedUrlCache>());
    }

    [Fact]
    public async Task ObjectPresignedUrl_SecondCallReturnsCachedUrl()
    {
        var (service, cache) = BuildService();
        var upload = await service
            .FromStream(new MemoryStream(new byte[10]), "photo.jpg")
            .UploadAsync("media");

        var first = await service
            .Object("media", upload.Value!.ObjectKey)
            .GetPresignedUrlAsync<DownloadPolicy>();
        var second = await service
            .Object("media", upload.Value.ObjectKey)
            .GetPresignedUrlAsync<DownloadPolicy>();

        Assert.Equal(first.Value, second.Value);
        var cached = await cache.GetAsync(
            "memory",
            "media",
            upload.Value.ObjectKey,
            typeof(DownloadPolicy).FullName!);
        Assert.Equal(first.Value, cached);
    }

    [Fact]
    public async Task UploadPresignedUrl_WarmsCache()
    {
        var (service, cache) = BuildService();
        var upload = await service
            .PresignedUrl<DownloadPolicy>()
            .FromStream(new MemoryStream(new byte[5]), "photo.jpg")
            .UploadAsync("media");

        var cached = await cache.GetAsync(
            "memory",
            "media",
            upload.Value!.ObjectKey,
            typeof(DownloadPolicy).FullName!);

        Assert.NotNull(cached);
        Assert.Contains(upload.Value.ObjectKey, cached);
    }
}
