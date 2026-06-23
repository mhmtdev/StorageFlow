---
applyTo: "src/**/*,tests/**/*,samples/**/*,benchmarks/**/*,StorageFlow.sln"
---

# StorageFlow Architecture Contract

This file is the detailed technical contract for StorageFlow. It complements
the repository-wide `.github/copilot-instructions.md` file. Apply both files;
do not choose between them.

## 1. Architectural Goal

StorageFlow is a provider-agnostic object storage framework for .NET.
Application and business code depend on `IStorageService`, never AWS, MinIO, or
RustFS SDK types.

The framework supports these first-party providers:

- AWS S3
- MinIO
- RustFS

Provider packages translate neutral StorageFlow contracts to SDK calls. Core
owns orchestration, policies, validation, naming, profiles, delivery URLs, and
provider resolution. Abstractions owns public contracts and neutral models.

## 2. Package Graph

The dependency graph is fixed:

```text
StorageFlow.Abstractions
    ↑
    ├── StorageFlow.Core
    ├── StorageFlow.Provider.S3
    ├── StorageFlow.Provider.Minio
    ├── StorageFlow.Provider.RustFs
    ├── StorageFlow.Extension.Redis
    └── StorageFlow.Testing (test-only, non-packable)
```

Rules:

- Abstractions has no project dependency and no provider SDK dependency.
- Core references Abstractions only.
- Each provider references Abstractions only, never Core or another provider.
- Each extension references Abstractions only, never Core or a provider.
- Circular references are forbidden.
- `StorageFlow.Testing` references Abstractions only and has
  `<IsPackable>false</IsPackable>`.
- Production projects, samples, and public packages never reference
  `StorageFlow.Testing`.

Provider-specific implementation belongs only in its provider package. Do not
move SDK adapters, SDK exception mapping, or provider options into Core.

## 3. Public API Shape

### 3.1 Fluent application entry point

`IStorageService` is the application entry point and remains mockable:

```csharp
public interface IStorageService : IStorageOperationBuilder
{
}
```

The operation builder supports:

```csharp
Provider(SFProvider.Minio)
Profile<TProfileKey>()
Validation<TPolicyKey>()
Naming<TPolicyKey>()
PresignedUrl<TPolicyKey>()
Metadata(key, value)
Metadata(values)
CacheControl(value)
ContentDisposition(value)
FromStream(stream, fileName, contentType, contentLength)
UploadAsync(bucket, cancellationToken)
UploadAsync(cancellationToken)
Object(bucket, objectKey)
Object(objectKey)
Objects(bucket, objectKeys)
Objects(objectKeys)
```

Rules:

- The builder is mutable per operation and must not copy the stream.
- Builder methods may be called in any order.
- When the same operation setting is supplied more than once, the last value
  wins.
- Cancellation tokens appear only on terminal asynchronous operations.
- Missing stream, filename, bucket, profile setting, provider, or policy returns
  a failed `StorageResult`; SDK exceptions do not escape terminal operations.
- The framework does not dispose caller upload streams.
- Do not add ASP.NET `IFormFile` dependencies to framework packages.
- Do not restore public `UploadOptions`, string provider APIs, `For<T>()`, or
  direct `UploadAsync(bucket, stream, options)` APIs.

### 3.2 Object operations

One-object operations use `IStorageObjectBuilder`:

```csharp
DownloadAsync(cancellationToken)
DeleteAsync(cancellationToken)
ExistsAsync(cancellationToken)
GetPresignedUrlAsync(cancellationToken)
GetPresignedUrlAsync<TPolicyKey>(cancellationToken)
GetDeliveryUrl<TPolicyKey>()
```

Batch public delivery uses `IStorageObjectCollectionBuilder`:

```csharp
GetDeliveryUrls<TPolicyKey>()
```

Do not mix upload methods into object builders or asynchronous provider I/O into
the synchronous delivery URL surface.

### 3.3 Result model

Terminal operations return `StorageResult` or `StorageResult<T>`.

Common error codes:

- `ValidationFailed`
- `ObjectNotFound`
- `BucketNotFound`
- `PermissionDenied`
- `ProviderError`
- `Unknown`

Provider SDK exceptions are wrapped in `StorageProviderException`, which owns
the common error code. Core forwards that code and must not infer behavior from
localized or unstable exception message text.

Registration and startup configuration are allowed to throw
`StorageConfigurationException` because they represent programmer errors.

## 4. Provider Registration and Selection

Provider selection uses the closed first-party token catalog:

```csharp
SFProvider.S3
SFProvider.Minio
SFProvider.RustFs
```

Applications cannot construct tokens, register provider factories, or provide
provider names as strings.

Each provider NuGet package extends `ProviderCollectionBuilder`:

```csharp
options.Providers.UseS3(s3 => s3.Configure(...));
options.Providers.UseMinio(minio => minio.Configure(...));
options.Providers.UseRustFs(rustFs => rustFs.Configure(...));
```

Registration rules:

- `Configure` is required exactly once per provider registration.
- Duplicate provider registration fails at startup.
- More than one explicit `.AsDefault()` fails at startup.
- One registered provider becomes the automatic default.
- Multiple providers require one explicit default or explicit operation/profile
  selection.
- The provider implementation `ProviderName` must match the token identity.
- Selecting a catalog token that was not registered returns an operation
  configuration failure.

Provider registration builders expose `Configure`, `Validation`, `Naming`,
`PresignedUrls`, and `DeliveryUrls`.

Only a maintainer-approved first-party package may add a provider. Adding one
requires a new Abstractions token and version; application-defined providers are
not part of the public extensibility model.

## 5. Strongly Typed Policies

Public policy selection uses marker interfaces:

```csharp
IValidationPolicyKey
INamingPolicyKey
IPresignedUrlPolicyKey
IDeliveryUrlPolicyKey
IStorageProfileKey
```

String policy and profile names are forbidden.

Global registration:

```csharp
options.Validation.AddPolicy<ImagePolicy>(...);
options.Naming.AddPolicy<MediaNaming>(...);
options.PresignedUrls.AddPolicy<DownloadPolicy>(...);
options.DeliveryUrls.AddPolicy<PublicDelivery>(...);
```

Provider override registration:

```csharp
minio.Validation.AddPolicy<ImagePolicy>(...);
minio.Naming.AddPolicy<MediaNaming>(...);
minio.PresignedUrls.AddPolicy<DownloadPolicy>(...);
minio.DeliveryUrls.AddPolicy<PublicDelivery>(...);
```

Resolution for a selected policy key:

1. Active provider override
2. Global policy
3. Configuration failure

A provider policy under the same key replaces the global policy completely.
Policy properties are not merged.

## 6. Upload Pipeline

The pipeline order is fixed:

```text
1. FileSizeValidator
2. ExtensionValidator
3. MimeTypeValidator
4. FileSignatureValidator
5. Naming strategy
6. Provider upload
7. Optional presigned URL cache warm-up
```

Validation always precedes naming; naming always precedes provider I/O. Do not
parallelize these stages.

### 6.1 Validation

`ValidationPolicy` supports:

- minimum and maximum size
- allowed and blocked extensions
- allowed MIME types
- required magic-number/file-signature validation

Known built-in signatures include JPEG, PNG, PDF, ZIP, MP3, and MP4.

Built-in validator order values must remain stable. Application validators
implement `IFileValidator`, define `Order`, and register through:

```csharp
services.AddStorageFlowValidator<TValidator>();
```

Custom validators are resolved from DI and merged with built-ins by `Order`.
They run only when the upload selects a validation policy.

For non-seekable streams:

- File-signature reads use bounded prefix replay.
- The entire object must never be buffered to restore the stream.
- A custom validator that consumes beyond the replayable prefix produces a
  validation failure instead of a truncated upload.
- Known `contentLength` remains available independently of `Stream.Length`.

### 6.2 Naming

Naming key selection order:

1. Explicit operation `Naming<TPolicyKey>()`
2. Global naming policy marked `.AsDefault()`
3. GUID fallback

After selecting the key, provider override still precedes global definition.
Provider-level defaults do not exist.

Built-in strategies:

- `GuidNamingStrategy`
- `SeoNamingStrategy`
- `PatternNamingStrategy`

Pattern tokens:

- `{yyyy}`
- `{MM}`
- `{dd}`
- `{guid}`
- `{slug}`
- `{ext}`
- `{timestamp}`

Patterns are immutable registration-time policy data. Upload operations cannot
supply raw pattern overrides. A different behavior requires a different typed
naming key.

Custom strategies implement `IFileNamingStrategy`, register through
`AddStorageFlowNamingStrategy<TStrategy>()`, and are resolved from DI.

Generated object keys must be non-empty relative forward-slash paths. Reject
backslashes, absolute keys, and `.`/`..` segments.

## 7. Storage Profiles

Profiles group provider, bucket, validation, naming, and presigned URL policy:

```csharp
options.Profiles.Add<MediaProfile>(profile => profile
    .Provider(SFProvider.Minio)
    .Bucket("media")
    .Validation<ImagePolicy>()
    .Naming<MediaNaming>()
    .PresignedUrl<DownloadPolicy>());
```

Rules:

- Profile keys implement `IStorageProfileKey`.
- Duplicate profile keys fail at startup.
- `UploadAsync(cancellationToken)` uses the profile bucket.
- `UploadAsync(bucket, cancellationToken)` overrides the profile bucket.
- `Object(objectKey)`/`Objects(objectKeys)` use the profile bucket.
- Explicit object bucket overloads override the profile bucket.
- Settings applied later in the fluent chain win.
- A profile without naming uses global default naming, then GUID fallback.
- Non-generic `GetPresignedUrlAsync` uses the profile presigned policy;
  generic selection overrides it.

## 8. Provider I/O Contract

`IStorageProvider.UploadAsync` receives:

- bucket
- generated object key
- caller-owned stream
- optional content type
- optional known content length
- optional custom metadata
- separate `UploadHeaders`
- cancellation token

Providers must forward known content length for non-seekable streams and must
not buffer full content to determine length. `UploadResult.SizeBytes` uses known
content length when available.

`UploadHeaders.CacheControl` and `ContentDisposition` are not custom metadata.
Blank or CR/LF header values fail before provider invocation. Repeated fluent
settings use the last value.

Successful upload returns bucket, object key, provider name, content type,
nullable size, normalized nullable ETag, and upload timestamp.

### 8.1 Streaming download

`DownloadResult` contains:

- caller-owned `Content` stream
- `ContentType`
- nullable `ContentLength`
- normalized nullable `ETag`
- nullable `LastModified`
- case-insensitive read-only user metadata

Core never disposes returned streams.

S3 and RustFS expose SDK response streams through delegating streams that also
dispose the owning SDK response.

MinIO performs stat first, then streams callback data through a bounded
`System.IO.Pipelines.Pipe`. Pipe backpressure is required. Disposing the reader
cancels producer transfer; producer failures propagate during reads. Do not
replace this with `MemoryStream` or full-object buffering.

### 8.2 Metadata and ETags

- Provider metadata prefixes do not appear in application keys.
- MinIO stat metadata accepts both `x-amz-meta-`-prefixed and already-normalized
  SDK keys.
- Download metadata is read-only and case-insensitive.
- Upload/download ETags are trimmed and have surrounding quotes removed.
- ETags are opaque provider values, never guaranteed content hashes.

### 8.3 Common error behavior

Providers map SDK failures to:

- missing object -> `ObjectNotFound`
- missing/inaccessible bucket -> `BucketNotFound`
- unauthorized/forbidden/signature/access key failures -> `PermissionDenied`
- other SDK/network failures -> `ProviderError`

`ExistsAsync` returns successful `false` for a missing object. Preserve
provider-native idempotent delete behavior. Core does not inspect message text.

## 9. Provider-Specific Contracts

### 9.1 AWS S3

- Standard AWS endpoints use the configured region and HTTPS.
- Both static access and secret omitted -> AWS SDK default credential chain.
- Exactly one static credential supplied -> startup configuration failure.
- Session token without static pair -> startup configuration failure.
- Static pair -> `BasicAWSCredentials`.
- Static pair plus session token -> `SessionAWSCredentials`.
- Optional custom `ServiceUrl` enables S3-compatible/local endpoints and force
  path style.
- Presigned URL protocol follows the endpoint scheme.

### 9.2 MinIO

- Explicit endpoint, access key, and secret key are required.
- MinIO SDK handles stat, bounded streaming download, exists/delete, and
  presigned URLs.
- Upload uses an AWS S3-compatible client against the same endpoint because
  MinIO .NET SDK 7.0.0 does not correctly emit `Content-Disposition` as the
  required HTTP content header.
- The upload client uses `ServiceURL`, force path style, and
  `AuthenticationRegion`.
- Keep metadata normalization compatible with prefixed and normalized SDK keys.

### 9.3 RustFS

- Explicit service URL, access key, and secret key are required.
- AWS S3 SDK is used against the custom endpoint.
- Configuration uses `ServiceURL`, `ForcePathStyle`, and
  `AuthenticationRegion`.
- Do not replace `AuthenticationRegion` with a standard AWS `RegionEndpoint`;
  that can produce invalid signatures.
- Presigned URL protocol follows the service URL scheme.

## 10. Presigned URLs and Cache

Presigned policies use `IPresignedUrlPolicyKey` and define expiration plus HTTP
method. V1 public examples use presigned GET for private download/access.

Redis is optional and exclusively caches presigned URLs. Core does not reference
Redis packages. The extension works through `IDistributedCache` and may either
register StackExchange Redis or reuse an existing distributed cache.

Cache key format:

```text
{prefix}{providerName}:{bucket}:{objectKey}:{policyKey}
```

`policyKey` is generated internally from the fully qualified marker type name.
Applications do not supply it as a string. TTL mirrors policy expiration unless
the extension has an explicit absolute expiration override.

Selecting `PresignedUrl<TPolicyKey>()` during upload only warms the optional
cache after provider upload. It does not upload content through a presigned URL.

## 11. Public CDN Delivery URLs

Delivery policy keys implement `IDeliveryUrlPolicyKey`. No default delivery
policy exists; callers always choose a typed key.

Policy resolution is provider override, then global. Runtime policy data is
validated, normalized, immutable, and safe for singleton concurrent reads.

Delivery generation is synchronous, deterministic, local, and network-free:

- no `Task` or fake async
- no worker thread or `Parallel.ForEach`
- no provider SDK
- no Redis
- no existence check
- no regex or LINQ in the hot batch loop
- no repeated base URL parsing

Batch input is `IReadOnlyList<string>`. Preallocate an output array with the
same count. Preserve order and duplicates. Invalid object keys produce
item-level `ObjectDeliveryUrlResult.Error`; one invalid key does not fail valid
items.

Production base URLs require HTTPS. HTTP is accepted only for localhost and
loopback. Reject base URL query/fragment, backslashes, absolute paths, and
`.`/`..` path segments. Encode path segments while preserving `/` separators.

Signed CDN URLs, invalidation, and storage existence checks are outside V1.

## 12. Dependency Injection and Lifetimes

- `IStorageService` is scoped.
- Provider registry/options/policy override data are singleton and immutable
  after startup.
- Built-in/custom naming strategies and validators resolve through DI.
- Do not resolve services through a global service locator.
- Do not retain operation builder state in the singleton registry or options.
- Policy resolution must not depend on global mutable application state.

## 13. Test Architecture

### Unit

`StorageFlow.Tests.Unit` tests isolated validators, naming, delivery algorithms,
configuration, SDK request mapping, streaming wrappers, cache behavior, and
results. It uses SDK mocks and does not reference `StorageFlow.Testing` or the
sample.

### Component

`StorageFlow.Tests.Component` owns Core/fluent end-to-end flows through the
test-only InMemory provider. It covers upload/object CRUD, policies, profiles,
routing, delivery, cache warm-up, configuration, and sample HTTP endpoints.

### Docker Integration

`StorageFlow.Tests.Integration` uses Testcontainers 4.12.0 and runs only when
`STORAGEFLOW_TEST_DOCKER=true`.

Pinned images:

| Service | Image |
|---|---|
| MinIO | `minio/minio:RELEASE.2025-09-07T16-13-09Z` |
| RustFS | `rustfs/rustfs:1.0.0-beta.7` |
| LocalStack | `localstack/localstack:4.14.0` |
| Redis | `redis:7.0` |

Fixtures use dynamic ports, ephemeral credentials, dedicated buckets, and
automatic cleanup. Without the flag, all Docker tests are explicitly skipped
and no container starts. Provider collections run sequentially; at most two
collections execute concurrently.

Provider contracts cover upload/download byte equality, non-seekable streams,
stream ownership, metadata, content headers, ETag, exists/delete, presigned GET,
missing resources, denied credentials, and MinIO early-dispose cancellation.
Redis covers set/get/remove, TTL, policy key separation, and upload warm-up.

### AWS Cloud

`StorageFlow.Tests.Cloud` runs only with `STORAGEFLOW_TEST_AWS_ENABLED=true` and
requires region plus an existing dedicated bucket. It leaves S3 static
credentials empty to exercise the AWS default credential chain. Local use is
through an AWS profile; CI uses GitHub OIDC/IAM role.

Each run uses `storageflow-tests/{run-id}/` and deletes created objects during
fixture cleanup. Never create or delete the bucket. A lifecycle rule is a safety
net, not a replacement for cleanup.

### Required validation

Unit and Component must pass without Docker/AWS and with zero skips. Docker
Integration is required in pull-request CI. AWS runs nightly/manual and is
required before stable release when S3 behavior changes.

## 14. Documentation, Security, and Distribution

- `README.md` is public product documentation and the readme packed into every
  public NuGet package.
- `CONTRIBUTING.md` defines issue-first contribution and PR expectations.
- `SECURITY.md` defines private vulnerability reporting. Never put suspected
  vulnerabilities in public issues.
- `LICENSE` is MIT. Every public package declares
  `<PackageLicenseExpression>MIT</PackageLicenseExpression>`.
- `tests/README.md` owns test environment instructions.
- Do not invent badges, repository URLs, contacts, or support promises.
- Never commit credentials, `.env`, local test settings, presigned URLs, or
  customer object keys/data.

Before publishing:

- Release build has zero errors and warnings.
- Unit/Component/Docker suites pass as required.
- Relevant AWS smoke tests pass.
- Public XML documentation is complete.
- SemVer 2.0 is used consistently across packages.
- Package metadata contains no placeholders.
- Direct/transitive vulnerability checks are clean.
- Deprecated dependencies are reviewed.
- Generated packages contain current README and MIT metadata.
- No package contains InMemory APIs or a `StorageFlow.Testing` dependency.

## 15. Adding a First-Party Provider

An approved provider requires:

1. `StorageFlow.Provider.{Name}` project.
2. `IStorageProvider` implementation.
3. New closed token in `SFProvider`.
4. `Use{Name}()` extension on `ProviderCollectionBuilder`.
5. Public registration builder exposing `Configure`, `Validation`, `Naming`,
   `PresignedUrls`, and `DeliveryUrls`.
6. Registration through the internal Abstractions contract.
7. Abstractions-only project reference.
8. Unit SDK mapping tests and real provider contract tests.
9. README/sample/package documentation updates.

## 16. V1 Non-Goals

Do not implement without an explicit scope decision:

- multipart/chunked upload
- range download
- object listing
- copy/move
- direct browser upload through presigned PUT
- queue/broker integrations
- image processing
- encryption management
- audit logging
- OpenTelemetry integration
- built-in retry policies

Designs may preserve future extensibility, but public documentation must not
promise a V2 feature or schedule.

## 17. Stable V1 Release Acceptance

Before publishing stable `1.0.0`:

1. Pull-request CI passes on the supported Linux runner.
2. Real AWS S3 cloud smoke tests pass against a dedicated test bucket.
3. All public NuGet package IDs and ownership are confirmed.
4. Authors, descriptions, tags, project URL, repository URL, and version
   metadata contain no placeholders.
5. The target-framework strategy is explicitly approved for the release.
6. Public XML documentation is complete and included where intended.
7. Direct and transitive vulnerability checks are clean.
8. Deprecated dependencies are upgraded or explicitly accepted.
9. Every package contains the current README and MIT metadata and contains no
   InMemory API or `StorageFlow.Testing` dependency.
10. Public API review confirms the fluent surface is ready for SemVer
    compatibility commitments.

V1 is successful when consumers can install only the providers they need,
switch providers without changing business logic, configure typed policies and
profiles, stream large objects without full buffering, handle common errors
without SDK coupling, use private presigned and public delivery URLs, add Redis
cache optionally, and test application flows without production storage.
