# StorageFlow

[![CI](https://github.com/mhmtdev/StorageFlow/actions/workflows/ci.yml/badge.svg)](https://github.com/mhmtdev/StorageFlow/actions/workflows/ci.yml)

[Getting Started](#quick-start) | [Packages](#packages) | [Contributing](CONTRIBUTING.md) | [Releasing](RELEASING.md) | [Security](SECURITY.md) | [MIT License](LICENSE)

StorageFlow is a provider-agnostic object storage framework for .NET. It keeps
application code independent from AWS S3, MinIO, and RustFS while providing one
fluent API for uploads, downloads, object operations, validation, naming,
presigned URLs, and public CDN delivery URLs.

StorageFlow is not a thin wrapper around a single SDK. Provider SDKs remain
behind `IStorageProvider`; application code works through `IStorageService` and
strongly typed policies.

```csharp
var result = await storage
    .Provider(SFProvider.Minio)
    .Validation<DocumentsPolicy>()
    .Metadata("source", "documents-api")
    .FromStream(stream, fileName, contentType, contentLength)
    .UploadAsync("documents", cancellationToken);
```

## Why StorageFlow?

- Use the same application API with AWS S3, MinIO, and RustFS.
- Change providers through configuration instead of business code.
- Define validation, naming, presigned URL, and delivery URL behavior as typed
  policies.
- Override policies for one provider without duplicating application logic.
- Stream uploads and downloads without buffering complete objects in memory.
- Group provider, bucket, and policies into reusable typed profiles.
- Cache presigned URLs through an optional Redis extension.
- Return consistent `StorageResult` errors instead of leaking provider SDK
  exceptions.

## Packages

| Package | Purpose |
|---|---|
| `StorageFlow.Core` | Fluent API, pipeline, policies, profiles, validation, and naming |
| `StorageFlow.Abstractions` | Public contracts and result models; referenced transitively by other packages |
| `StorageFlow.Provider.S3` | AWS S3 provider and S3-compatible custom endpoints |
| `StorageFlow.Provider.Minio` | MinIO provider |
| `StorageFlow.Provider.RustFs` | RustFS provider |
| `StorageFlow.Extension.Redis` | Optional presigned URL cache |

Install Core and only the provider packages your application needs:

```bash
dotnet add package StorageFlow.Core
dotnet add package StorageFlow.Provider.Minio
```

For AWS S3 or RustFS, replace the provider package accordingly:

```bash
dotnet add package StorageFlow.Provider.S3
dotnet add package StorageFlow.Provider.RustFs
```

## Quick Start

### 1. Define typed policy keys

Policy keys are empty marker types. They replace error-prone string names and
are discoverable through the compiler.

```csharp
using StorageFlow.Abstractions.Interfaces;

public sealed class DocumentsPolicy : IValidationPolicyKey;
public sealed class MediaNaming : INamingPolicyKey;
public sealed class DownloadUrlPolicy : IPresignedUrlPolicyKey;
public sealed class PublicAssetDelivery : IDeliveryUrlPolicyKey;
```

### 2. Register StorageFlow

```csharp
using StorageFlow.Core.DependencyInjection;
using StorageFlow.Provider.Minio;

builder.Services.AddStorageFlow(options =>
{
    options.Validation.AddPolicy<DocumentsPolicy>(policy =>
    {
        policy.MaxFileSizeBytes = 10 * 1024 * 1024;
        policy.AllowedExtensions = [".pdf", ".zip"];
        policy.AllowedMimeTypes = ["application/pdf", "application/zip"];
        policy.RequireValidSignature = true;
    });

    options.Naming
        .AddPolicy<MediaNaming>(policy =>
            policy.UsePattern("{yyyy}/{MM}/{slug}-{guid}{ext}"))
        .AsDefault();

    options.PresignedUrls.AddPolicy<DownloadUrlPolicy>(policy =>
    {
        policy.Expiration = TimeSpan.FromMinutes(15);
        policy.HttpMethod = HttpMethod.Get;
    });

    options.DeliveryUrls.AddPolicy<PublicAssetDelivery>(policy => policy
        .UseCdn("https://cdn.example.com")
        .WithPathPrefix("assets"));

    options.Providers.UseMinio(minio => minio.Configure(config =>
    {
        config.Endpoint = builder.Configuration["Storage:Minio:Endpoint"]!;
        config.AccessKey = builder.Configuration["Storage:Minio:AccessKey"]!;
        config.SecretKey = builder.Configuration["Storage:Minio:SecretKey"]!;
        config.UseSSL = true;
    })).AsDefault();
});
```

`AsDefault()` is optional when exactly one provider is registered. Mark a
provider explicitly when registering more than one.

### 3. Upload an object

```csharp
var result = await storage
    .Validation<DocumentsPolicy>()
    .CacheControl("private, max-age=0")
    .ContentDisposition("attachment")
    .Metadata("source", "documents-api")
    .FromStream(stream, fileName, contentType, contentLength)
    .UploadAsync("documents", cancellationToken);

if (!result.IsSuccess)
{
    logger.LogWarning(
        "Storage operation failed: {Code} {Message}",
        result.Error!.Code,
        result.Error.Message);
    return;
}

var objectKey = result.Value!.ObjectKey;
var etag = result.Value.ETag;
```

The global default naming policy is used automatically. If no naming policy is
selected and no global default exists, StorageFlow falls back to GUID naming.

## Provider Configuration

### AWS S3

```csharp
using StorageFlow.Core.DependencyInjection;
using StorageFlow.Provider.S3;

builder.Services.AddStorageFlow(options =>
{
    options.Providers.UseS3(s3 => s3.Configure(config =>
    {
        config.Region = builder.Configuration["Storage:S3:Region"]
            ?? "eu-central-1";
    })).AsDefault();
});
```

When access and secret keys are omitted, the AWS SDK default credential chain
is used. This is the recommended configuration for IAM roles, workload
identity, local AWS profiles, and CI federation.

Static or temporary credentials are also supported:

```csharp
builder.Services.AddStorageFlow(options =>
{
    options.Providers.UseS3(s3 => s3.Configure(config =>
    {
        config.Region = builder.Configuration["Storage:S3:Region"]
            ?? "eu-central-1";
        config.AccessKey = builder.Configuration["Storage:S3:AccessKey"];
        config.SecretKey = builder.Configuration["Storage:S3:SecretKey"];
        config.SessionToken = builder.Configuration["Storage:S3:SessionToken"];
    })).AsDefault();
});
```

Access and secret keys must be supplied together. A session token requires
both values.

### MinIO

```csharp
using StorageFlow.Core.DependencyInjection;
using StorageFlow.Provider.Minio;

builder.Services.AddStorageFlow(options =>
{
    options.Providers.UseMinio(minio => minio.Configure(config =>
    {
        config.Endpoint = builder.Configuration["Storage:Minio:Endpoint"]
            ?? "localhost:9000";
        config.AccessKey = builder.Configuration["Storage:Minio:AccessKey"]!;
        config.SecretKey = builder.Configuration["Storage:Minio:SecretKey"]!;
        config.UseSSL = false;
        config.Region = "us-east-1";
    })).AsDefault();
});
```

MinIO requires explicit access and secret keys. Keep them in user secrets,
environment variables, or a production secret manager rather than source
control.

### RustFS

```csharp
using StorageFlow.Core.DependencyInjection;
using StorageFlow.Provider.RustFs;

builder.Services.AddStorageFlow(options =>
{
    options.Providers.UseRustFs(rustFs => rustFs.Configure(config =>
    {
        config.ServiceUrl = builder.Configuration["Storage:RustFs:ServiceUrl"]
            ?? "http://localhost:9000";
        config.AccessKey = builder.Configuration["Storage:RustFs:AccessKey"]!;
        config.SecretKey = builder.Configuration["Storage:RustFs:SecretKey"]!;
        config.Region = "us-east-1";
        config.ForcePathStyle = true;
    })).AsDefault();
});
```

RustFS also requires an explicit access/secret pair. Use HTTPS for production
service URLs.

### Multiple providers

Register all required providers and mark one as the default. Select another
provider for an operation with the closed `SFProvider` catalog:

```csharp
using StorageFlow.Core.DependencyInjection;
using StorageFlow.Provider.Minio;
using StorageFlow.Provider.S3;

builder.Services.AddStorageFlow(options =>
{
    options.Providers.UseMinio(minio => minio.Configure(config =>
    {
        config.Endpoint = builder.Configuration["Storage:Minio:Endpoint"]!;
        config.AccessKey = builder.Configuration["Storage:Minio:AccessKey"]!;
        config.SecretKey = builder.Configuration["Storage:Minio:SecretKey"]!;
        config.UseSSL = true;
    })).AsDefault();

    options.Providers.UseS3(s3 => s3.Configure(config =>
    {
        config.Region = builder.Configuration["Storage:S3:Region"]
            ?? "eu-central-1";
    }));
});

var result = await storage
    .Provider(SFProvider.S3)
    .Validation<DocumentsPolicy>()
    .FromStream(stream, fileName, contentType, contentLength)
    .UploadAsync("documents", cancellationToken);
```

Provider selection is made by application code, profiles, or configuration. It
should not be accepted as an arbitrary string from an HTTP client.

## Object Operations

### Streaming download

```csharp
var result = await storage
    .Provider(SFProvider.Minio)
    .Object("documents", objectKey)
    .DownloadAsync(cancellationToken);

if (result.IsSuccess)
{
    var download = result.Value!;
    await using var content = download.Content;
    await content.CopyToAsync(destination, cancellationToken);
}
```

`DownloadResult` contains:

- `Content`: readable stream owned by the caller
- `ContentType`
- `ContentLength`
- normalized `ETag`
- `LastModified`
- case-insensitive, read-only user metadata

StorageFlow does not dispose download streams. S3 and RustFS expose wrapped SDK
response streams. MinIO uses a bounded pipe with backpressure and does not copy
the complete object into memory.

### Exists and delete

```csharp
var exists = await storage
    .Object("documents", objectKey)
    .ExistsAsync(cancellationToken);

var deleted = await storage
    .Object("documents", objectKey)
    .DeleteAsync(cancellationToken);
```

Delete is idempotent. A missing object produces a successful delete operation;
`ExistsAsync` returns a successful `false` result.

## Typed Policies

Policies can be registered globally or overridden for a provider. Resolution
uses the provider override first and then the global definition. A provider
policy registered under the same key replaces the global policy completely; it
does not merge individual settings.

### Validation

Validation policies support:

- minimum and maximum size
- allowed and blocked extensions
- allowed MIME types
- magic-number/file-signature validation

```csharp
options.Validation.AddPolicy<DocumentsPolicy>(policy =>
{
    policy.MinFileSizeBytes = 1;
    policy.MaxFileSizeBytes = 10 * 1024 * 1024;
    policy.AllowedExtensions = [".pdf"];
    policy.AllowedMimeTypes = ["application/pdf"];
    policy.RequireValidSignature = true;
});
```

Built-in signature validation recognizes JPEG, PNG, PDF, ZIP, MP3, and MP4.
Validation runs before naming and before the provider receives the stream.

Provider override example:

```csharp
options.Providers.UseMinio(minio =>
{
    minio.Configure(config => { /* connection settings */ });
    minio.Validation.AddPolicy<DocumentsPolicy>(policy =>
    {
        policy.MaxFileSizeBytes = 25 * 1024 * 1024;
        policy.AllowedExtensions = [".pdf"];
        policy.AllowedMimeTypes = ["application/pdf"];
        policy.RequireValidSignature = true;
    });
});
```

### Naming

Naming policies support GUID, SEO-friendly, pattern-based, and custom
strategies:

```csharp
public sealed class GuidNaming : INamingPolicyKey;
public sealed class SeoNaming : INamingPolicyKey;
public sealed class CustomNaming : INamingPolicyKey;

options.Naming.AddPolicy<GuidNaming>(policy => policy.UseGuid());
options.Naming.AddPolicy<SeoNaming>(policy => policy.UseSeo());
options.Naming.AddPolicy<MediaNaming>(policy =>
    policy.UsePattern("{yyyy}/{MM}/{dd}/{slug}-{guid}{ext}"));
options.Naming.AddPolicy<CustomNaming>(policy =>
    policy.UseStrategy<TenantNamingStrategy>());
```

Pattern tokens:

| Token | Value |
|---|---|
| `{yyyy}` | Four-digit year |
| `{MM}` | Two-digit month |
| `{dd}` | Two-digit day |
| `{guid}` | Eight-character GUID segment |
| `{slug}` | SEO-friendly original filename without extension |
| `{ext}` | Original extension including the dot |
| `{timestamp}` | Unix timestamp in seconds |

Register custom strategies through dependency injection:

```csharp
services.AddStorageFlowNamingStrategy<TenantNamingStrategy>();
```

Select a non-default naming policy explicitly when an operation requires it:

```csharp
await storage
    .Naming<SeoNaming>()
    .FromStream(stream, fileName, contentType, contentLength)
    .UploadAsync("media", cancellationToken);
```

Object keys are validated before upload. They must be relative, use forward
slashes, and cannot contain empty, `.` or `..` path segments.

### Presigned URLs

Presigned URL policies define private, time-limited access:

```csharp
options.PresignedUrls.AddPolicy<DownloadUrlPolicy>(policy =>
{
    policy.Expiration = TimeSpan.FromMinutes(15);
    policy.HttpMethod = HttpMethod.Get;
});

var result = await storage
    .Object("documents", objectKey)
    .GetPresignedUrlAsync<DownloadUrlPolicy>(cancellationToken);
```

Presigned URLs are provider-generated and may involve SDK work. They are
different from public delivery URLs.

### Public delivery URLs

Delivery URL policies generate deterministic CDN URLs without contacting the
storage provider, Redis, or the network:

```csharp
options.DeliveryUrls.AddPolicy<PublicAssetDelivery>(policy => policy
    .UseCdn("https://cdn.example.com")
    .WithPathPrefix("blog")
    .IncludeBucket(false));

var single = storage
    .Object("media", objectKey)
    .GetDeliveryUrl<PublicAssetDelivery>();

var batch = storage
    .Objects("media", objectKeys)
    .GetDeliveryUrls<PublicAssetDelivery>();
```

Batch generation preserves input order and duplicates. An invalid object key
is reported on its own `ObjectDeliveryUrlResult`; it does not fail otherwise
valid items in the batch. Production CDN base URLs must use HTTPS. HTTP is
accepted only for localhost and loopback development addresses.

## Storage Profiles

Profiles group a provider, bucket, and policies behind one typed key:

```csharp
public sealed class MediaProfile : IStorageProfileKey;

options.Profiles.Add<MediaProfile>(profile => profile
    .Provider(SFProvider.Minio)
    .Bucket("media")
    .Validation<DocumentsPolicy>()
    .Naming<MediaNaming>()
    .PresignedUrl<DownloadUrlPolicy>());
```

Use the profile without repeating its bucket and policies:

```csharp
var upload = await storage
    .Profile<MediaProfile>()
    .FromStream(stream, fileName, contentType, contentLength)
    .UploadAsync(cancellationToken);

var download = await storage
    .Profile<MediaProfile>()
    .Object(objectKey)
    .DownloadAsync(cancellationToken);
```

Fluent settings are order-aware: when the same setting is applied more than
once, the last value wins.

## Presigned URL Caching

Install the optional extension:

```bash
dotnet add package StorageFlow.Extension.Redis
```

Register Redis after StorageFlow:

```csharp
using StorageFlow.Extension.Redis;

builder.Services
    .AddStorageFlow(options => { /* providers and policies */ })
    .UseRedisPresignedUrlCache(redis =>
    {
        redis.ConnectionString = builder.Configuration.GetConnectionString("Redis")!;
        redis.KeyPrefix = "myapp:storage:presigned:";
    });
```

Redis is used only for presigned URL caching. StorageFlow Core has no Redis
dependency and works without the extension. An existing `IDistributedCache`
registration can be reused with `UseDistributedPresignedUrlCache()`.

Selecting a presigned policy during upload warms the optional cache:

```csharp
await storage
    .PresignedUrl<DownloadUrlPolicy>()
    .FromStream(stream, fileName, contentType, contentLength)
    .UploadAsync("documents", cancellationToken);
```

This does not upload through a presigned URL. It uploads through the selected
provider and caches a URL for later reads.

## Custom Validation

Implement `IFileValidator`, assign an `Order`, and register it with dependency
injection:

```csharp
services.AddStorageFlowValidator<MalwareScanValidator>();
```

Custom validators participate in the same ordered upload pipeline as the
built-in validators. A validator must leave the stream readable from its
original position. StorageFlow protects the replayable signature prefix for
non-seekable streams and rejects validators that would cause truncated uploads.

## Error Handling

Storage operations return `StorageResult` or `StorageResult<T>`:

```csharp
if (!result.IsSuccess)
{
    switch (result.Error!.Code)
    {
        case StorageErrorCode.ValidationFailed:
            break;
        case StorageErrorCode.ObjectNotFound:
            break;
        case StorageErrorCode.BucketNotFound:
            break;
        case StorageErrorCode.PermissionDenied:
            break;
        case StorageErrorCode.ProviderError:
            break;
    }
}
```

SDK-specific exceptions do not escape the application API. Provider failures
are normalized through `StorageProviderException` and a common
`StorageErrorCode`.

## Operational Notes

- Upload streams remain owned by the caller and are not disposed by
  StorageFlow.
- Download streams remain owned by the caller and must be disposed.
- Provide `contentLength` for non-seekable upload streams when it is known.
- ETags are normalized without surrounding quotes, but they are not guaranteed
  to be content hashes.
- Bucket creation and lifecycle management are application or infrastructure
  responsibilities.
- StorageFlow does not include a built-in retry policy. Apply retries at the
  application boundary with a resilience library appropriate for the workload.
- Keep access keys, secret keys, session tokens, and Redis connection strings in
  user secrets, environment variables, workload identity, or a secret manager.

## Samples and Tests

This repository intentionally contains one small, source-linked API sample at
[`samples/StorageFlow.Sample.Api`](samples/StorageFlow.Sample.Api). It includes
upload, streaming download, delete, exists, presigned URL, and delivery URL
endpoints and is used to smoke-test repository changes.

The Advanced Sample has been moved out of this repository so it can consume
the published NuGet packages like a real application. It is intentionally not
a project or solution dependency of StorageFlow. A public repository link will
be added here when that sample is published.

The test architecture is documented in [`tests/README.md`](tests/README.md):

```bash
dotnet test tests/StorageFlow.Tests.Unit
dotnet test tests/StorageFlow.Tests.Component

STORAGEFLOW_TEST_DOCKER=true \
dotnet test tests/StorageFlow.Tests.Integration
```

Docker integration tests run against real MinIO, RustFS, LocalStack, and Redis
containers. AWS cloud smoke tests are opt-in and use the AWS SDK default
credential chain.

## Contributing

Bug reports, design discussions, documentation improvements, and focused pull
requests are welcome. Read [CONTRIBUTING.md](CONTRIBUTING.md) before starting a
substantial change.

## Security

Do not report suspected vulnerabilities in a public issue. Follow the private
reporting process in [SECURITY.md](SECURITY.md).

## License

StorageFlow is licensed under the [MIT License](LICENSE).
