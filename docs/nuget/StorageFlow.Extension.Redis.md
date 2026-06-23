# StorageFlow.Extension.Redis

Optional Redis and `IDistributedCache` integration for caching StorageFlow
presigned URLs. StorageFlow works without this package.

Redis is not used for uploads, downloads, delivery URLs, metadata, provider
routing, or storage existence checks.

## Install

```bash
dotnet add package StorageFlow.Extension.Redis
```

## Use Redis

Register StorageFlow first, then enable the cache:

```csharp
using StorageFlow.Extension.Redis;

builder.Services
    .AddStorageFlow(options =>
    {
        // Register providers and presigned URL policies.
    })
    .UseRedisPresignedUrlCache(redis =>
    {
        redis.ConnectionString =
            builder.Configuration.GetConnectionString("Redis")!;
        redis.KeyPrefix = "storageflow:presigned:";
    });
```

Keep the connection string in a secret provider. When an application already
registers `IDistributedCache`, reuse it instead:

```csharp
builder.Services.UseDistributedPresignedUrlCache(redis =>
{
    redis.KeyPrefix = "storageflow:presigned:";
});
```

Cache entries are separated by provider, bucket, object key, and typed
presigned URL policy. By default, cache expiration mirrors the policy URL
lifetime. An optional `AbsoluteExpiration` value can override that TTL; choose
an override that does not outlive the generated URL.

See the [`StorageFlow.Core` user guide](https://www.nuget.org/packages/StorageFlow.Core)
and [GitHub repository](https://github.com/mhmtdev/StorageFlow).
