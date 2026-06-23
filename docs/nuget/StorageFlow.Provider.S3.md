# StorageFlow.Provider.S3

Official AWS S3 provider for StorageFlow. It supports uploads, streaming
downloads, delete, exists, object metadata, normalized ETags, and presigned
URLs through the common StorageFlow API.

## Install

```bash
dotnet add package StorageFlow.Core
dotnet add package StorageFlow.Provider.S3
```

## Recommended AWS configuration

Omit static credentials to use the AWS SDK default credential chain. This is
recommended for IAM roles, workload identity, AWS profiles, and CI federation.

```csharp
using StorageFlow.Core.DependencyInjection;
using StorageFlow.Provider.S3;

builder.Services.AddStorageFlow(options =>
{
    options.Providers.UseS3(s3 => s3.Configure(config =>
    {
        config.Region = "eu-north-1";
    })).AsDefault();
});
```

Static and temporary credentials are also supported:

```csharp
config.AccessKey = builder.Configuration["Storage:S3:AccessKey"];
config.SecretKey = builder.Configuration["Storage:S3:SecretKey"];
config.SessionToken = builder.Configuration["Storage:S3:SessionToken"];
```

Access and secret keys must be supplied together. A session token requires
both. Keep credentials outside source control.

## Select S3 for an operation

```csharp
var result = await storage
    .Provider(SFProvider.S3)
    .FromStream(stream, fileName, contentType, contentLength)
    .UploadAsync("documents", cancellationToken);
```

Downloads expose the AWS response stream through a wrapper that also owns the
SDK response. Dispose `DownloadResult.Content` when finished.

StorageFlow does not create buckets and does not include a built-in retry
policy. Configure bucket lifecycle, access, and resiliency at the application
or infrastructure boundary.

See the [`StorageFlow.Core` user guide](https://www.nuget.org/packages/StorageFlow.Core)
and [GitHub repository](https://github.com/mhmtdev/StorageFlow).
