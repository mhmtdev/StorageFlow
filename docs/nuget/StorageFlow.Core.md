# StorageFlow.Core

StorageFlow is a provider-agnostic object storage framework for .NET. It gives
application code one fluent API for AWS S3, MinIO, and RustFS while keeping
provider SDKs outside business logic.

Use StorageFlow when you want typed configuration, reusable policies, streaming
I/O, consistent results, and the freedom to select a registered provider per
operation.

## Install

Install Core and at least one provider package:

```bash
dotnet add package StorageFlow.Core
dotnet add package StorageFlow.Provider.Minio
```

Available providers:

- [`StorageFlow.Provider.S3`](https://www.nuget.org/packages/StorageFlow.Provider.S3)
- [`StorageFlow.Provider.Minio`](https://www.nuget.org/packages/StorageFlow.Provider.Minio)
- [`StorageFlow.Provider.RustFs`](https://www.nuget.org/packages/StorageFlow.Provider.RustFs)

## Quick start

Define strongly typed policy keys:

```csharp
using StorageFlow.Abstractions.Interfaces;

public sealed class DocumentsPolicy : IValidationPolicyKey;
public sealed class MediaNaming : INamingPolicyKey;
public sealed class DownloadUrl : IPresignedUrlPolicyKey;
public sealed class PublicAssets : IDeliveryUrlPolicyKey;
```

Register policies and a provider:

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

    options.PresignedUrls.AddPolicy<DownloadUrl>(policy =>
    {
        policy.Expiration = TimeSpan.FromMinutes(15);
        policy.HttpMethod = HttpMethod.Get;
    });

    options.DeliveryUrls.AddPolicy<PublicAssets>(policy => policy
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

Upload a stream:

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
    logger.LogWarning("{Code}: {Message}",
        result.Error!.Code,
        result.Error.Message);
    return;
}

var objectKey = result.Value!.ObjectKey;
var etag = result.Value.ETag;
```

The upload stream remains owned by the caller. StorageFlow does not dispose it.

## Validation

Validation policies can enforce:

- minimum and maximum file size;
- allowed and blocked extensions;
- allowed MIME types;
- magic-number/file-signature checks.

Extension and MIME allowlists are configuration driven. You can list any
extension or MIME type required by your application.

Built-in magic-number validation currently recognizes:

| Format | Supported extensions | Signature |
|---|---|---|
| JPEG | `.jpg`, `.jpeg` | JPEG SOI prefix |
| PNG | `.png` | PNG signature |
| PDF | `.pdf` | `%PDF` prefix |
| ZIP | `.zip` | Standard, empty, and spanning ZIP prefixes |
| MP3 | `.mp3` | MPEG frame prefixes or ID3 tag |
| MP4 | `.mp4` | ISO base media `ftyp` box |

`RequireValidSignature = true` validates known formats. For an extension not in
the table, StorageFlow currently skips the signature step; use a custom
`IFileValidator` when that format must be verified. Extension and MIME checks
still run normally.

Custom validators are resolved from dependency injection and ordered by their
`Order` value:

```csharp
builder.Services.AddStorageFlowValidator<MalwareScanValidator>();
```

## Naming

Naming policies support GUID, SEO, pattern, and custom strategies:

```csharp
options.Naming.AddPolicy<GuidNaming>(policy => policy.UseGuid());
options.Naming.AddPolicy<SeoNaming>(policy => policy.UseSeo());
options.Naming.AddPolicy<MediaNaming>(policy =>
    policy.UsePattern("{yyyy}/{MM}/{dd}/{slug}-{guid}{ext}"));
```

Pattern tokens: `{yyyy}`, `{MM}`, `{dd}`, `{guid}`, `{slug}`, `{ext}`, and
`{timestamp}`.

An operation uses its explicit naming policy, then the global default, then a
GUID fallback. Provider-level definitions override a global policy with the
same key.

## Object operations

Download content as a stream:

```csharp
var result = await storage
    .Object("documents", objectKey)
    .DownloadAsync(cancellationToken);

if (result.IsSuccess)
{
    var download = result.Value!;
    await using var content = download.Content;
    await content.CopyToAsync(destination, cancellationToken);
}
```

`DownloadResult` includes content type, content length, normalized ETag, last
modified time, and read-only user metadata. The caller owns the returned
stream.

```csharp
var exists = await storage
    .Object("documents", objectKey)
    .ExistsAsync(cancellationToken);

var deleted = await storage
    .Object("documents", objectKey)
    .DeleteAsync(cancellationToken);
```

## Private and public URLs

Use a presigned URL for temporary private access:

```csharp
var signed = await storage
    .Object("documents", objectKey)
    .GetPresignedUrlAsync<DownloadUrl>(cancellationToken);
```

Use a delivery URL for stable public CDN addresses. Delivery URL generation is
synchronous and performs no storage or network request:

```csharp
var publicUrl = storage
    .Object("media", objectKey)
    .GetDeliveryUrl<PublicAssets>();
```

Batch delivery preserves input ordering and duplicate keys:

```csharp
var urls = storage
    .Objects("media", objectKeys)
    .GetDeliveryUrls<PublicAssets>();
```

## Multiple providers

Register several official providers and mark one default. Select another with
the autocomplete-friendly `SFProvider` catalog:

```csharp
var result = await storage
    .Provider(SFProvider.S3)
    .Validation<DocumentsPolicy>()
    .FromStream(stream, fileName, contentType, contentLength)
    .UploadAsync("documents", cancellationToken);
```

Provider selection should come from application logic or a typed profile, not
from an arbitrary client-supplied string.

## Results and errors

Public operations return `StorageResult` or `StorageResult<T>`. Provider SDK
exceptions do not escape the API. Common error codes include
`ObjectNotFound`, `BucketNotFound`, `PermissionDenied`, `ProviderError`,
`ValidationFailed`, and `Unknown`.

ETags are normalized without surrounding quotes, but an ETag is not guaranteed
to be a content hash.

## More information

- [GitHub repository](https://github.com/mhmtdev/StorageFlow)
- [Minimal API sample](https://github.com/mhmtdev/StorageFlow/tree/main/samples/StorageFlow.Sample.Api)
- [AI agent integration guide](https://github.com/mhmtdev/StorageFlow/tree/main/docs/ai)
- [Contributing](https://github.com/mhmtdev/StorageFlow/blob/main/CONTRIBUTING.md)
- [Security policy](https://github.com/mhmtdev/StorageFlow/blob/main/SECURITY.md)

StorageFlow targets .NET 9 and later and is licensed under MIT.
