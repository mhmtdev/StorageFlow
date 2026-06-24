# StorageFlow Consumer Agent Instructions

Use these rules when generating or modifying application code that consumes
StorageFlow NuGet packages. They describe the supported public API and expected
application behavior.

## Objective

StorageFlow keeps application code independent from AWS S3, MinIO, and RustFS.
Application services use `IStorageService`; provider SDK types must not cross
into business or application layers.

Prefer the framework's typed policies, fluent operations, result models, and
official provider registration APIs. Do not recreate provider routing,
validation, naming, presigned URL, or delivery URL behavior around an SDK.

## Package selection

Install `StorageFlow.Core` and only the official providers or extensions the
application needs:

```bash
dotnet add package StorageFlow.Core
dotnet add package StorageFlow.Provider.S3
dotnet add package StorageFlow.Provider.Minio
dotnet add package StorageFlow.Provider.RustFs
dotnet add package StorageFlow.Extension.Redis
```

Rules:

- At least one provider package is required for storage operations.
- Do not add `StorageFlow.Abstractions` explicitly unless the project only needs
  contracts; Core and provider packages bring it transitively.
- Do not reference provider SDKs from application services to bypass
  StorageFlow.
- Do not install or reference `StorageFlow.Testing` in production or sample
  applications.
- Redis is optional and is used only for presigned URL caching.

## Dependency injection

Register StorageFlow once during application startup:

```csharp
builder.Services.AddStorageFlow(options =>
{
    options.Providers.UseMinio(minio => minio.Configure(config =>
    {
        config.Endpoint = builder.Configuration["Storage:Minio:Endpoint"]!;
        config.AccessKey = builder.Configuration["Storage:Minio:AccessKey"]!;
        config.SecretKey = builder.Configuration["Storage:Minio:SecretKey"]!;
        config.UseSSL = true;
    })).AsDefault();
});
```

Provider rules:

- Register providers only with `options.Providers.UseS3`, `UseMinio`, or
  `UseRustFs`.
- Use `.AsDefault()` when multiple providers are registered and one should
  handle operations without explicit selection.
- A single registered provider becomes the implicit default.
- Select a provider with `storage.Provider(SFProvider.S3)`,
  `SFProvider.Minio`, or `SFProvider.RustFs`.
- Never accept a provider name or token directly from an HTTP request. Translate
  an application-owned decision to an `SFProvider` token.
- Applications cannot create provider tokens or register custom provider
  factories.

## Credentials

- AWS S3 uses the AWS SDK default credential chain when access and secret keys
  are both omitted. Prefer IAM roles, workload identity, profiles, or CI OIDC.
- AWS access and secret keys are an all-or-nothing pair. A session token
  requires both values.
- MinIO and RustFS require explicit access and secret keys.
- Keep credentials and Redis connection strings in user secrets, environment
  variables, workload identity, or a production secret manager.
- Never write credentials to source files, committed settings, logs, errors,
  tests, or agent instructions.

## Typed policy keys

Public application APIs use marker types, not string policy names:

```csharp
public sealed class DocumentsPolicy : IValidationPolicyKey;
public sealed class MediaNaming : INamingPolicyKey;
public sealed class DownloadUrl : IPresignedUrlPolicyKey;
public sealed class PublicAssets : IDeliveryUrlPolicyKey;
public sealed class MediaProfile : IStorageProfileKey;
```

Rules:

- Define one empty marker class for each distinct application policy.
- Register global policies on `StorageFlowOptions`.
- Register provider-specific overrides inside the provider registration.
- Policy resolution is provider override first, then global policy.
- A provider override replaces the global policy under the same key; settings
  are not merged.
- Missing explicitly selected policies are configuration failures. Do not catch
  those failures and silently substitute unrelated behavior.

## Fluent upload API

Use a single fluent chain ending in a terminal operation:

```csharp
StorageResult<UploadResult> result = await storage
    .Provider(SFProvider.Minio)
    .Validation<DocumentsPolicy>()
    .CacheControl("private, max-age=0")
    .ContentDisposition("attachment")
    .Metadata("source", "documents-api")
    .FromStream(stream, fileName, contentType, contentLength)
    .UploadAsync(bucket, cancellationToken);
```

Rules:

- Keep files as `Stream`; StorageFlow Core has no ASP.NET `IFormFile`
  dependency.
- Pass the original file name and content type when available.
- Pass `contentLength` for non-seekable streams when known.
- The caller owns the upload stream. StorageFlow never disposes it.
- Add metadata with `.Metadata(...)`.
- Add `Cache-Control` and `Content-Disposition` through their dedicated fluent
  methods, not as custom metadata.
- Blank or CR/LF-containing standard header values are invalid.
- The last fluent value wins when the same operation setting is applied more
  than once.
- Do not retain or reuse a partially configured operation builder across
  requests. Build and execute the chain within the operation scope.

## Validation

Validation policies support minimum/maximum size, extension allow/block lists,
MIME allowlists, and file-signature checks.

```csharp
options.Validation.AddPolicy<DocumentsPolicy>(policy =>
{
    policy.MinFileSizeBytes = 1;
    policy.MaxFileSizeBytes = 10 * 1024 * 1024;
    policy.AllowedExtensions = [".pdf", ".zip"];
    policy.AllowedMimeTypes = ["application/pdf", "application/zip"];
    policy.RequireValidSignature = true;
});
```

Extension and MIME policies can contain any values required by the application.
Built-in magic-number validation recognizes only:

- `.jpg`, `.jpeg`
- `.png`
- `.pdf`
- `.zip`
- `.mp3`
- `.mp4`

When `RequireValidSignature` is true and the extension is not in this list, the
built-in signature validator skips the signature check. Extension and MIME
checks still run. Add a custom `IFileValidator` for a format that requires a
signature check not built into StorageFlow:

```csharp
builder.Services.AddStorageFlowValidator<CustomFormatValidator>();
```

Do not claim that renaming protection applies to unknown formats. Do not buffer
the entire stream merely to inspect a short signature.

## Naming

Register naming behavior under typed keys:

```csharp
options.Naming
    .AddPolicy<MediaNaming>(policy =>
        policy.UsePattern("{yyyy}/{MM}/{slug}-{guid}{ext}"))
    .AsDefault();
```

Supported policy strategies:

```csharp
policy.UseGuid();
policy.UseSeo();
policy.UsePattern("{yyyy}/{MM}/{dd}/{slug}-{guid}{ext}");
policy.UseStrategy<CustomNamingStrategy>();
```

Pattern tokens are `{yyyy}`, `{MM}`, `{dd}`, `{guid}`, `{slug}`, `{ext}`, and
`{timestamp}`.

Rules:

- If an operation does not call `.Naming<T>()`, use the global default policy.
- If no global default exists, StorageFlow falls back to GUID naming.
- Use `.Naming<T>()` only to select a different registered policy.
- Do not accept naming patterns from clients and do not provide per-upload raw
  pattern overrides.
- Register custom strategies with
  `AddStorageFlowNamingStrategy<TStrategy>()`.
- Object keys must be non-empty relative paths, use forward slashes, and must
  not contain empty, `.` or `..` segments.

## Downloads and object operations

Use the object builder:

```csharp
StorageResult<DownloadResult> result = await storage
    .Provider(SFProvider.Minio)
    .Object(bucket, objectKey)
    .DownloadAsync(cancellationToken);
```

Rules:

- `DownloadAsync` returns object content; it does not create a presigned URL.
- The caller owns `DownloadResult.Content` and must dispose it.
- Do not copy the complete object into memory unless the application explicitly
  requires a byte array and accepts that allocation.
- Use `DownloadResult.ContentType` and `ContentLength` when producing an HTTP
  response.
- `DownloadResult` can also contain normalized ETag, last-modified time, and
  read-only user metadata.
- An ETag is not guaranteed to be a content hash.

```csharp
var exists = await storage.Object(bucket, objectKey)
    .ExistsAsync(cancellationToken);

var deleted = await storage.Object(bucket, objectKey)
    .DeleteAsync(cancellationToken);
```

`ExistsAsync` returns successful `false` for a missing object. Delete is
idempotent in normal application use.

## Presigned URLs and delivery URLs

These APIs solve different problems.

Use a presigned URL for temporary provider-authorized private access:

```csharp
var result = await storage
    .Object(bucket, objectKey)
    .GetPresignedUrlAsync<DownloadUrl>(cancellationToken);
```

Use a delivery URL for stable public CDN addresses:

```csharp
var result = storage
    .Object(bucket, objectKey)
    .GetDeliveryUrl<PublicAssets>();
```

Rules:

- Presigned operations are asynchronous provider operations.
- Delivery URL generation is synchronous, deterministic, and local.
- Delivery URL generation must not be wrapped in `Task.Run`, parallel loops, or
  fake async APIs.
- Delivery URL generation does not call storage, Redis, or a network service and
  does not prove that an object exists.
- Batch delivery accepts an `IReadOnlyList<string>`, preserves input order and
  duplicates, and reports invalid keys per item.
- Use HTTPS CDN base URLs in production. HTTP is development-only for localhost
  and loopback addresses.
- Signed CDN URLs and CDN invalidation are outside v1.

Redis may cache presigned URLs only. Do not use
`StorageFlow.Extension.Redis` as an object, metadata, or delivery URL cache.

## Typed profiles

Use a profile to group provider, bucket, and policies:

```csharp
options.Profiles.Add<MediaProfile>(profile => profile
    .Provider(SFProvider.Minio)
    .Bucket("media")
    .Validation<DocumentsPolicy>()
    .Naming<MediaNaming>()
    .PresignedUrl<DownloadUrl>());
```

```csharp
var result = await storage
    .Profile<MediaProfile>()
    .FromStream(stream, fileName, contentType, contentLength)
    .UploadAsync(cancellationToken);
```

Operation settings applied later in the chain override profile settings.

## Result handling

Public operations return `StorageResult` or `StorageResult<T>`. Check
`IsSuccess` before reading `Value`:

```csharp
if (!result.IsSuccess)
{
    logger.LogWarning("Storage failure {Code}: {Message}",
        result.Error!.Code,
        result.Error.Message);
    return;
}
```

Supported error codes are:

- `ValidationFailed`
- `ObjectNotFound`
- `BucketNotFound`
- `PermissionDenied`
- `ProviderError`
- `Unknown`

Do not catch AWS, MinIO, or S3-compatible SDK exceptions in application code.
Provider exceptions are normalized by StorageFlow.

## Performance and reliability

- Use async terminal methods for provider I/O.
- Do not wrap provider calls in `Task.Run`.
- Do not add parallelism to a single upload or download stream.
- Avoid whole-object buffering.
- Propagate the request cancellation token to terminal operations.
- StorageFlow does not provide a built-in retry policy. Add workload-appropriate
  resilience at the application boundary and avoid retrying non-repeatable
  streams blindly.
- StorageFlow does not manage buckets. Provision buckets and lifecycle rules as
  infrastructure.

## V1 boundaries

Do not generate code that assumes StorageFlow v1 implements:

- multipart or chunked upload;
- range download;
- bucket management;
- queue or broker integrations;
- image processing;
- encryption management;
- audit logging;
- OpenTelemetry integration;
- built-in retry policies;
- signed CDN URLs or CDN invalidation.

Use application or infrastructure services for these concerns until a public
StorageFlow release explicitly adds them.

## Reference material

- `docs/storageflow/examples.md`
- `docs/storageflow/troubleshooting.md`
- https://github.com/mhmtdev/StorageFlow
