# StorageFlow Copilot Instructions

These repository-wide instructions apply to GitHub Copilot Chat, coding agent,
and code review. Follow them before proposing or changing code.

## Read First

Use these sources in this order:

1. The user's current request.
2. The nearest `AGENTS.md` file.
3. `.github/instructions/storageflow-architecture.instructions.md` for the
   detailed technical contract, V1 boundaries, and release acceptance.
4. The current implementation and tests.

If two sources conflict, do not guess. Report the conflict before implementing.

## Project Intent

StorageFlow is a provider-agnostic object storage framework for .NET. It
supports AWS S3, MinIO, and RustFS while keeping application code independent
from provider SDKs.

Application code uses `IStorageService`. Provider packages implement
`IStorageProvider`. StorageFlow is not a thin AWS SDK wrapper.

## Working Rules

- Inspect the affected projects, tests, and public contracts before editing.
- Prefer existing patterns and keep changes narrowly scoped.
- Do not perform unrelated refactoring or formatting.
- Do not introduce a dependency, target-framework change, or public API change
  incidentally.
- Preserve nullable reference type correctness.
- Add XML documentation to every public member.
- Keep interfaces and application-facing abstractions mockable.
- Do not introduce service locators, global mutable state, or generic static
  helper classes.
- Do not expose provider SDK types through Core or application-facing APIs.
- Never commit credentials, presigned URLs, `.env` files, local settings, or
  secret-manager output.

## Package Boundaries

| Project | Allowed project dependencies |
|---|---|
| `StorageFlow.Abstractions` | None |
| `StorageFlow.Core` | Abstractions only |
| `StorageFlow.Provider.*` | Abstractions only; never Core |
| `StorageFlow.Extension.*` | Abstractions only; never Core or providers |
| `StorageFlow.Testing` | Abstractions only; must remain non-packable |

`StorageFlow.Testing`, `InMemoryStorageProvider`, and `UseInMemory()` are test
infrastructure. They must never appear in production projects, samples, or
public NuGet packages.

## Public API Guardrails

- Provider registration uses `options.Providers.UseS3/UseMinio/UseRustFs(...)`.
- Provider selection uses the closed `SFProvider` catalog.
- Do not add string provider selection, `For<T>()`, public provider-token
  construction, or application provider factories.
- Application operations use the fluent `IStorageService` API.
- Do not restore public `UploadOptions` or direct upload overloads.
- Validation, naming, presigned URL, delivery URL, and profile selection use
  strongly typed marker keys. String policy/profile names are forbidden.
- Provider policy overrides replace the global policy registered under the same
  key; properties are not merged.
- Terminal storage operations return `StorageResult`/`StorageResult<T>`.
- Invalid startup registration may throw `StorageConfigurationException`.
- SDK exceptions must be wrapped in `StorageProviderException` and mapped to a
  provider-independent `StorageErrorCode`.

## Upload and Stream Rules

The upload sequence is fixed:

1. Size validation
2. Extension validation
3. MIME validation
4. File-signature validation
5. Naming
6. Provider upload
7. Optional presigned URL cache warm-up

- Do not reorder the pipeline.
- Custom validators are resolved from DI and ordered by `IFileValidator.Order`.
- Non-seekable signature validation uses bounded prefix replay. Full-object
  buffering and truncated uploads are forbidden.
- Forward known `contentLength` to providers; do not read an entire stream to
  calculate it.
- Upload and download streams are caller-owned. StorageFlow must not dispose
  them.
- S3/RustFS downloads wrap the SDK response stream. MinIO downloads use a
  bounded `System.IO.Pipelines.Pipe` and early disposal cancels transfer.
- Metadata is case-insensitive and read-only on download. Provider metadata
  prefixes must not leak.
- `Cache-Control` and `Content-Disposition` are separate from custom metadata
  and must reject blank or CR/LF values.
- Normalize ETags without quotes, but never treat them as guaranteed hashes.

## Provider Rules

- AWS S3 uses the SDK default credential chain when static credentials are
  omitted. Access/secret are an all-or-nothing pair; session token requires the
  pair.
- MinIO and RustFS require explicit credentials.
- MinIO upload uses the S3-compatible request client; stat, download, and
  presigned URL operations use the MinIO SDK. Keep bounded-pipe download.
- MinIO metadata must handle both `x-amz-meta-`-prefixed and SDK-normalized keys.
- RustFS custom endpoint signing uses `ServiceURL`, `ForcePathStyle`, and
  `AuthenticationRegion`.
- S3 and RustFS presigned URL schemes follow the configured endpoint. Do not
  hardcode HTTP or HTTPS globally.
- Map provider failures to `ObjectNotFound`, `BucketNotFound`,
  `PermissionDenied`, or `ProviderError` without parsing exception messages in
  Core.

## Delivery URL and Redis Rules

- Delivery URLs are synchronous, deterministic, and network-free.
- Delivery generation must not use tasks, threads, parallel loops, provider
  SDKs, Redis, or existence checks.
- Batch delivery accepts `IReadOnlyList<string>`, preserves order and
  duplicates, preallocates output, and reports invalid keys per item.
- Production delivery base URLs require HTTPS; HTTP is local/loopback only.
- Redis is optional and used only for presigned URL caching through
  `IDistributedCache`. Core must not depend on Redis packages.

## Tests

Use the narrowest correct layer:

- Unit: isolated algorithms and SDK mocks; no Testing/sample references.
- Component: fluent/Core flows through `StorageFlow.Testing`.
- Integration: real MinIO, RustFS, LocalStack, and Redis via Testcontainers.
- Cloud: opt-in real AWS S3 tests using the default credential chain.

Required commands for ordinary changes:

```bash
dotnet build StorageFlow.sln -c Release --no-restore
dotnet test tests/StorageFlow.Tests.Unit
dotnet test tests/StorageFlow.Tests.Component
```

Run Docker contracts for provider, streaming, metadata, cache, or integration
changes:

```bash
STORAGEFLOW_TEST_DOCKER=true \
dotnet test tests/StorageFlow.Tests.Integration
```

AWS cloud tests require explicit opt-in, region, and a dedicated existing
bucket. Never invent or infer a production bucket.

## Documentation, Security, and Distribution

- Update `README.md` and the minimal sample when public usage changes.
- Update `tests/README.md` when test setup changes.
- Update the architecture instruction when a mandatory technical contract
  changes, including V1 scope or release acceptance criteria.
- Do not place speculative V2 promises in the public README.
- Security vulnerabilities must follow `SECURITY.md` and must not be disclosed
  in public issues.
- Contributions follow `CONTRIBUTING.md` and are licensed under MIT.
- Public packages must declare MIT, contain the current README, and contain no
  Testing dependency or InMemory API.
- Placeholder NuGet metadata such as `Package Description` is forbidden for a
  release.

## V1 Boundaries

Do not implement multipart/chunked upload, range download, object listing,
copy/move, direct browser upload through presigned PUT, queue integrations,
image processing, encryption management, audit logging, OpenTelemetry, or a
built-in retry layer unless the maintainer explicitly changes V1 scope.
