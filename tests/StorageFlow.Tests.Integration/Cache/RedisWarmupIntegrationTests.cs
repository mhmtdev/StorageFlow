using Microsoft.Extensions.DependencyInjection;
using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Tests.Integration.Fixtures;
using StorageFlow.Tests.Integration.Infrastructure;

namespace StorageFlow.Tests.Integration.Cache;

[Collection(CollectionName)]
public sealed class RedisWarmupIntegrationTests(
    MinioFixture minio,
    RedisFixture redis)
{
    public const string CollectionName = "MinIO with Redis";

    [DockerFact]
    public async Task Upload_WarmsRealRedisCache()
    {
        var prefix = $"storageflow:warmup:{Guid.NewGuid():N}:";
        await using var services = minio.CreateServicesWithRedis(
            redis.ConnectionString,
            prefix);
        var storage = services.GetRequiredService<IStorageService>();

        var upload = await storage
            .PresignedUrl<IntegrationDownloadPolicy>()
            .FromStream(new MemoryStream([1, 2, 3]), "photo.jpg")
            .UploadAsync(minio.Bucket);

        Assert.True(upload.IsSuccess, upload.Error?.Message);
        var cache = services.GetRequiredService<IPresignedUrlCache>();
        var cached = await cache.GetAsync(
            "minio",
            minio.Bucket,
            upload.Value!.ObjectKey,
            typeof(IntegrationDownloadPolicy).FullName!);
        Assert.False(string.IsNullOrWhiteSpace(cached));

        await storage.Object(minio.Bucket, upload.Value.ObjectKey).DeleteAsync();
    }
}

[CollectionDefinition(RedisWarmupIntegrationTests.CollectionName)]
public sealed class RedisWarmupCollection :
    ICollectionFixture<MinioFixture>,
    ICollectionFixture<RedisFixture>;
