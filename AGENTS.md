# StorageFlow - AGENTS.md

This file defines mandatory behavioral rules for all AI agents (GitHub Copilot, Claude, etc.)
working on the StorageFlow project. Read this file before generating any code.

---

## Project Summary

StorageFlow is a provider-agnostic Object Storage Framework for .NET.
It runs on AWS S3, MinIO, and RustFS; application code never depends directly on any SDK.

Before changing code, read the sources relevant to the task:

- `.github/copilot-instructions.md`: repository-wide Copilot rules
- `.github/instructions/storageflow-architecture.instructions.md`: detailed
  technical architecture contract, V1 scope, and release acceptance criteria

This file contains mandatory cross-agent day-to-day rules. If instruction
sources conflict, stop and report the conflict before implementing.

---

## Package Dependency Rules

The following dependency rules must never be violated:

| Package | Allowed Dependencies |
|---|---|
| `StorageFlow.Abstractions` | None |
| `StorageFlow.Core` | `Abstractions` only |
| `StorageFlow.Provider.*` | `Abstractions` only — **never** Core |
| `StorageFlow.Extension.*` | `Abstractions` only — **never** Core or any Provider |
| `StorageFlow.Testing` | `Abstractions` only; `<IsPackable>false</IsPackable>` is mandatory |
| `StorageFlow.Tests.Unit` | Production projects only; never Testing or samples |
| `StorageFlow.Tests.Component` | May reference `StorageFlow.Testing` and the minimal sample |
| `StorageFlow.Tests.Integration` | Production providers/extensions; never Testing |
| `StorageFlow.Tests.Cloud` | Production S3 packages; never Testing |

**Circular dependencies are not allowed at any level.**

A reference to `StorageFlow.Testing` must never be added to:
- Production projects
- Sample applications
- Published NuGet packages

---

## File and Class Placement

Follow the project root structure when creating new files:

```
StorageFlow/
├── src/
│   ├── StorageFlow.Abstractions/   ← interfaces, models, exceptions, attributes
│   ├── StorageFlow.Core/           ← pipeline, validation, naming, registry, DI
│   ├── StorageFlow.Provider.S3/
│   ├── StorageFlow.Provider.Minio/
│   ├── StorageFlow.Provider.RustFs/
│   └── StorageFlow.Extension.Redis/
├── tests/
│   ├── StorageFlow.Testing/        ← test infrastructure only, not packable
│   ├── StorageFlow.Tests.Unit/
│   ├── StorageFlow.Tests.Component/
│   ├── StorageFlow.Tests.Integration/
│   └── StorageFlow.Tests.Cloud/
└── samples/
    └── StorageFlow.Sample.Api/
```

Provider-specific code must never be placed inside `Core` or `Abstractions`.

---

## Code Generation Rules

### General

- Add XML doc comments to all public members (`/// <summary>`)
- `<Nullable>enable</Nullable>` is active; respect null-safety throughout
- The `sealed` keyword must **not** be applied to public abstractions
- Static helper classes and the service locator pattern are **forbidden**
- Do not produce god classes; keep classes small and single-responsibility

### Interfaces and Abstractions

- Define an interface first for every new behavior
- Place the implementation in a separate class
- `IStorageService`, `IStorageProvider`, `IFileValidator`, `IFileNamingStrategy` —
  these must remain mockable (must not be sealed or static)

### StorageResult Usage

Terminal application storage operations return `StorageResult` or
`StorageResult<T>` instead of throwing SDK exceptions directly:

```csharp
// CORRECT
public async Task<StorageResult<UploadResult>> UploadAsync(...) { ... }

// WRONG — throws exception directly
public async Task<UploadResult> UploadAsync(...) { throw new Exception(...); }
```

Provider exceptions are always wrapped as `StorageProviderException`;
SDK-specific exceptions must never leak into application code.

Startup configuration builders may throw `StorageConfigurationException` for
invalid registrations and contradictory settings.

### Provider Tokens

Provider selection uses the closed first-party `SFProvider` token catalog:

```csharp
options.Providers.UseMinio(minio => minio.Configure(...)).AsDefault();
storage.Provider(SFProvider.Minio)
    .FromStream(stream, fileName)
    .UploadAsync(bucket);
```

Applications must not create provider tokens or register provider factories.
Provider registration is available only through official
`StorageFlow.Provider.*` NuGet packages. Generic keys, string selection, and
custom application providers are forbidden.

### Upload Pipeline Order

Upload pipeline steps execute in a fixed order — this order must not be changed:

1. `FileSizeValidator`
2. `ExtensionValidator`
3. `MimeTypeValidator`
4. `FileSignatureValidator` (Magic Number)
5. Naming Strategy
6. Provider Upload
7. Presigned URL Cache (optional)

Each step must be independently testable and replaceable.

### Validation

Magic Number (file signature) validation is mandatory — it blocks files whose extensions have been renamed.
When adding a new validator, implement `IFileValidator` and set the `Order` property accordingly.
Register application validators with `AddStorageFlowValidator<TValidator>()`;
they are resolved from DI and merged with built-in validators by `Order`.
Signature validation on non-seekable streams must use bounded prefix replay;
full-object memory buffering and truncated provider uploads are forbidden.

### Policy Resolution Order

Policies must use strongly-typed marker keys:

```csharp
public sealed class ImagePolicy : IValidationPolicyKey;
public sealed class DownloadPolicy : IPresignedUrlPolicyKey;
public sealed class MediaNaming : INamingPolicyKey;
public sealed class BlogImageDelivery : IDeliveryUrlPolicyKey;
```

String policy names are forbidden in public application APIs. Register and use
policies through generic keys:

```csharp
options.Validation.AddPolicy<ImagePolicy>(...);
provider.Validation.AddPolicy<ImagePolicy>(...);
options.Naming.AddPolicy<MediaNaming>(policy => policy.UsePattern(...));
provider.Naming.AddPolicy<MediaNaming>(policy => policy.UsePattern(...));
storage.Validation<ImagePolicy>()
    .FromStream(stream, fileName)
    .UploadAsync(bucket);
storage.Object(bucket, objectKey).GetPresignedUrlAsync<DownloadPolicy>();
storage.Object(bucket, objectKey).GetDeliveryUrl<BlogImageDelivery>();
```

When resolving a policy key during an operation:

1. Provider-level policies of the active provider
2. Global policies
3. If not found in either → `StorageConfigurationException`

### Naming Strategies

Naming policies use `INamingPolicyKey`. Naming selection order is explicit
operation policy, global default policy, then GUID fallback. After a policy key
is selected, provider override takes precedence over the global definition.

String naming strategy identifiers are forbidden in public application APIs.
Select policies with `Naming<TPolicyKey>()`. Naming configuration is immutable
for an operation: patterns and strategies must be registered under policy keys,
not supplied during upload.

A global default naming policy is selected only with:

```csharp
options.Naming.AddPolicy<MediaNaming>(...).AsDefault();
```

When a global naming policy is marked as default, normal uploads must not
repeat `.Naming<MediaNaming>()`. Explicit naming selection is reserved for
choosing a different registered policy key.

Provider-level naming builders must not expose or emulate a default policy.

Built-in strategies: `GuidNamingStrategy`, `SeoNamingStrategy`,
`PatternNamingStrategy`. To add a new strategy, implement
`IFileNamingStrategy`, register it with
`AddStorageFlowNamingStrategy<TStrategy>()`, and select it from a naming policy.

Generated object keys must be non-empty and relative, use forward slashes, and
must not contain `.` or `..` path segments.

Available pattern tokens: `{yyyy}`, `{MM}`, `{dd}`, `{guid}`, `{slug}`, `{ext}`, `{timestamp}`

### Provider I/O

Downloads return `StorageResult<DownloadResult>`. `DownloadResult.Content` is a
stream owned by the caller; Core must never dispose it. S3 and RustFS expose the
SDK response stream through a wrapper that also owns the SDK response. MinIO
uses a bounded `System.IO.Pipelines.Pipe`; full-object buffering is forbidden,
and disposing the consumer stream must cancel the producer transfer.

Download metadata is case-insensitive and read-only. Provider metadata prefixes
must not leak into application keys. Upload `Cache-Control` and
`Content-Disposition` remain separate from custom metadata. Header values must
reject blanks and CR/LF before provider invocation.

The MinIO provider uses the MinIO SDK for stat, download, and presigned URLs.
Its upload path uses an S3-compatible request client because MinIO .NET SDK
7.0.0 cannot correctly emit `Content-Disposition` as an HTTP content header.
Do not move MinIO downloads away from the bounded pipe when changing this
upload implementation.

Provider SDK failures must map to `ObjectNotFound`, `BucketNotFound`,
`PermissionDenied`, or `ProviderError` through `StorageProviderException`.
Core must use the exception code directly and must not infer codes from message
text. `ExistsAsync` keeps returning successful `false` for a missing object.

Upload and download ETags are normalized without surrounding quotes. An ETag
must never be documented or treated as a guaranteed content hash.

AWS S3 uses the SDK default credential chain when both static credential fields
are omitted. Static access and secret keys are an all-or-nothing pair; a session
token requires that pair. MinIO and RustFS continue to require explicit
credentials.

S3 and RustFS presigned URL protocols must follow the configured service URL.
Standard AWS endpoints default to HTTPS; HTTP is allowed only when an explicit
custom endpoint uses HTTP. Do not hardcode provider-wide HTTP or HTTPS behavior.

### Delivery URLs

Typed delivery URL policies generate stable public CDN URLs for list pages,
images, CSS, and other public assets. Resolution order is provider override,
then global policy. There is no default delivery policy; callers must always
select an `IDeliveryUrlPolicyKey`.

Delivery URL generation is synchronous, deterministic, and local. It must not
use `Task`, worker threads, parallel loops, provider SDK calls, Redis, existence
checks, or any network request. Batch APIs accept `IReadOnlyList<string>`,
preserve ordering and duplicates, preallocate the result array, and report
invalid object keys as item-level errors.

Production base URLs require HTTPS. HTTP is allowed only for localhost and
loopback development addresses. Prefixes, buckets, and object keys must be
relative forward-slash paths without `.` or `..` segments.

### Redis Extension

Redis is used exclusively for Presigned URL caching.
The `Core` package must never depend on Redis packages.
The extension operates through `IDistributedCache` — the framework works without Redis.

Cache key format: `{prefix}{providerName}:{bucket}:{objectKey}:{policyKey}`

`policyKey` is an internal cache identity derived from the policy key type's
fully-qualified name. Applications must not provide it as a string.

---

## Adding a New Provider

When an AI agent generates code for a new provider, follow these steps:

1. Create the `StorageFlow.Provider.{Name}` project
2. Implement `IStorageProvider`
3. Add the provider token to `SFProvider` in `StorageFlow.Abstractions`
4. Add a `Use{Name}()` extension method on `ProviderCollectionBuilder`
5. Add a public `{Name}RegistrationBuilder` with `Configure`, `Validation`,
   `Naming`, `PresignedUrls`, and `DeliveryUrls`
6. Register through the internal Abstractions registration API
7. Depend only on `StorageFlow.Abstractions` — depending on Core is **forbidden**

---

## Test Writing Rules

- `StorageFlow.Tests.Unit` contains isolated tests and SDK mocks; it must not
  reference `StorageFlow.Testing` or a sample project
- `StorageFlow.Tests.Component` owns Core/fluent flows through
  `InMemoryStorageProvider`
- `StorageFlow.Tests.Integration` owns opt-in Testcontainers tests for MinIO,
  RustFS, LocalStack, and Redis
- `StorageFlow.Tests.Cloud` owns opt-in real AWS S3 smoke tests
- `InMemoryStorageProvider` is used only in `StorageFlow.Tests.Component` and
  test infrastructure
- `UseInMemory()` must not appear in production code or samples
- Docker credentials are ephemeral test fixture values and must not be supplied
  by developers or committed
- AWS cloud tests use the SDK default credential chain, an existing test bucket,
  a unique run prefix, and cleanup; static AWS credentials must not be committed
- Tests requiring Docker or AWS must be explicitly gated and mandatory in their
  dedicated CI jobs
- Every validator must be independently testable
- Every naming strategy must be testable with deterministic inputs
- `IStorageService` must be mockable via Moq or NSubstitute

Test coverage targets:
- All validators — both individually and in combination
- All built-in naming strategies (including edge cases)
- Presigned URL policy resolution
- Delivery URL global/provider resolution, encoding, and item-level failures
- Delivery URL batch sizes 1, 100, 1,000, and 10,000
- Profile resolution
- `StorageResult` success and failure paths
- Full CRUD cycle via the in-memory provider
- Streaming download ownership, MinIO cancellation, and transfer error propagation
- Provider object metadata and common error-code mapping
- Upload standard headers and normalized ETags
- AWS default, static, and session credential modes
- Real provider CRUD, metadata/header, presigned GET, missing-resource, and
  stream behavior through Docker contract tests
- Real Redis roundtrip, TTL, policy-key separation, and upload cache warm-up

---

## V1 Boundaries

Do not implement multipart/chunked upload, range download, object listing,
copy/move, direct browser upload through presigned PUT, queue integrations,
image processing, encryption management, audit logging, OpenTelemetry, or a
built-in retry layer unless the maintainer explicitly changes V1 scope.

---

## NuGet Package Checklist

Before publishing a package:

- [ ] `StorageFlow.Core.nupkg` contains no `InMemoryStorageProvider` or `UseInMemory()`
- [ ] `StorageFlow.Testing` project has `<IsPackable>false</IsPackable>`
- [ ] No public NuGet package references `StorageFlow.Testing`
- [ ] All public members have XML doc comments
- [ ] SemVer 2.0 is applied
- [ ] No circular dependencies exist
