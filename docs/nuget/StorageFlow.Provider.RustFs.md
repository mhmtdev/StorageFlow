# StorageFlow.Provider.RustFs

Official RustFS provider for StorageFlow. It supports S3-compatible uploads,
streaming downloads, delete, exists, object metadata, normalized ETags, and
presigned URLs through the common StorageFlow API.

## Install

```bash
dotnet add package StorageFlow.Core
dotnet add package StorageFlow.Provider.RustFs
```

## Configure

```csharp
using StorageFlow.Core.DependencyInjection;
using StorageFlow.Provider.RustFs;

builder.Services.AddStorageFlow(options =>
{
    options.Providers.UseRustFs(rustFs => rustFs.Configure(config =>
    {
        config.ServiceUrl = builder.Configuration["Storage:RustFs:ServiceUrl"]!;
        config.AccessKey = builder.Configuration["Storage:RustFs:AccessKey"]!;
        config.SecretKey = builder.Configuration["Storage:RustFs:SecretKey"]!;
        config.Region = "us-east-1";
        config.ForcePathStyle = true;
    })).AsDefault();
});
```

RustFS requires explicit credentials. Use an HTTPS service URL in production
and keep credentials outside source control.

## Select RustFS for an operation

```csharp
var result = await storage
    .Provider(SFProvider.RustFs)
    .FromStream(stream, fileName, contentType, contentLength)
    .UploadAsync("documents", cancellationToken);
```

Downloads are streamed without full-object buffering. The caller owns and must
dispose `DownloadResult.Content`.

StorageFlow does not create buckets and does not include a built-in retry
policy.

See the [`StorageFlow.Core` user guide](https://www.nuget.org/packages/StorageFlow.Core)
and [GitHub repository](https://github.com/mhmtdev/StorageFlow).
