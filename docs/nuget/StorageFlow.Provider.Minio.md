# StorageFlow.Provider.Minio

Official MinIO provider for StorageFlow. It supports uploads, bounded streaming
downloads, delete, exists, object metadata, normalized ETags, and presigned
URLs through the common StorageFlow API.

## Install

```bash
dotnet add package StorageFlow.Core
dotnet add package StorageFlow.Provider.Minio
```

## Configure

```csharp
using StorageFlow.Core.DependencyInjection;
using StorageFlow.Provider.Minio;

builder.Services.AddStorageFlow(options =>
{
    options.Providers.UseMinio(minio => minio.Configure(config =>
    {
        config.Endpoint = builder.Configuration["Storage:Minio:Endpoint"]!;
        config.AccessKey = builder.Configuration["Storage:Minio:AccessKey"]!;
        config.SecretKey = builder.Configuration["Storage:Minio:SecretKey"]!;
        config.UseSSL = true;
        config.Region = "us-east-1";
    })).AsDefault();
});
```

MinIO requires explicit access and secret keys. Store them in user secrets,
environment variables, Kubernetes/Docker secrets, or a production secret
manager.

## Select MinIO for an operation

```csharp
var result = await storage
    .Provider(SFProvider.Minio)
    .FromStream(stream, fileName, contentType, contentLength)
    .UploadAsync("documents", cancellationToken);
```

MinIO downloads use a bounded pipe with backpressure. The complete object is
not copied into memory. Disposing `DownloadResult.Content` cancels an active
transfer; the caller always owns that stream.

StorageFlow does not create buckets and does not include a built-in retry
policy.

See the [`StorageFlow.Core` user guide](https://www.nuget.org/packages/StorageFlow.Core)
and [GitHub repository](https://github.com/mhmtdev/StorageFlow).
