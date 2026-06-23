# StorageFlow.Abstractions

Public contracts and result models shared by the StorageFlow framework and its
official provider packages.

Most applications should install [`StorageFlow.Core`](https://www.nuget.org/packages/StorageFlow.Core)
and an official provider instead of referencing this package directly. The
provider packages bring Abstractions transitively.

## What it contains

- `IStorageService` and fluent operation contracts
- `IStorageProvider` provider boundary
- `StorageResult`, `StorageResult<T>`, and common error models
- upload and streaming download result models
- typed validation, naming, presigned URL, delivery URL, and profile keys
- official `SFProvider` tokens
- policy configuration models and builders

## Typed policy keys

Application policies use empty marker classes instead of string identifiers:

```csharp
using StorageFlow.Abstractions.Interfaces;

public sealed class DocumentsPolicy : IValidationPolicyKey;
public sealed class MediaNaming : INamingPolicyKey;
public sealed class DownloadUrl : IPresignedUrlPolicyKey;
public sealed class PublicAssets : IDeliveryUrlPolicyKey;
public sealed class MediaProfile : IStorageProfileKey;
```

This package does not contain provider SDK implementations or the upload
pipeline. Install Core and one of these packages for application use:

- [`StorageFlow.Provider.S3`](https://www.nuget.org/packages/StorageFlow.Provider.S3)
- [`StorageFlow.Provider.Minio`](https://www.nuget.org/packages/StorageFlow.Provider.Minio)
- [`StorageFlow.Provider.RustFs`](https://www.nuget.org/packages/StorageFlow.Provider.RustFs)

See the [StorageFlow repository](https://github.com/mhmtdev/StorageFlow) for the
architecture, API sample, contribution guide, and security policy.
