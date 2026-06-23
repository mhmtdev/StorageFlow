# StorageFlow Minimal Sample API

This sample is the quick-start kept in the main repository. It stays small and
uses project references so framework changes can be smoke-tested with the
solution.

## Endpoints

```text
POST   /api/storage/upload
GET    /api/storage/download/{objectKey}
DELETE /api/storage/{objectKey}
GET    /api/storage/exists/{objectKey}
GET    /api/storage/presigned/{objectKey}
GET    /api/storage/delivery/{objectKey}
```

The upload endpoint demonstrates:

- `Validation<DocumentsPolicy>()`
- global default `MediaNaming`
- `CacheControl(...)`
- `ContentDisposition(...)`
- custom metadata
- `UploadResult.ETag`

The download endpoint demonstrates `DownloadResult.Content` plus content type
and content length.

## Endpoint Organization

Each feature is kept in a small endpoint module:

```text
Endpoints/
├── UploadEndpoints.cs          Upload pipeline example
├── DownloadEndpoints.cs        Streaming download example
├── ObjectEndpoints.cs          Exists and delete examples
├── AccessUrlEndpoints.cs       Presigned and delivery URL examples
├── StorageHttpResults.cs       Shared StorageResult-to-HTTP mapping
└── StorageEndpointRouteBuilderExtensions.cs
                                Route-group composition only
```

## Secrets

Credentials stay out of `appsettings.json`.

For MinIO:

```bash
dotnet user-secrets set \
  "StorageFlow:Minio:AccessKey" "<access-key>" \
  --project samples/StorageFlow.Sample.Api

dotnet user-secrets set \
  "StorageFlow:Minio:SecretKey" "<secret-key>" \
  --project samples/StorageFlow.Sample.Api
```

For AWS S3 static credentials:

```bash
dotnet user-secrets set \
  "StorageFlow:S3:AccessKey" "<access-key>" \
  --project samples/StorageFlow.Sample.Api

dotnet user-secrets set \
  "StorageFlow:S3:SecretKey" "<secret-key>" \
  --project samples/StorageFlow.Sample.Api
```

If S3 access and secret keys are both empty, the AWS SDK default credential
chain is used.

`Minio.Enabled` is true by default. To try the same minimal endpoints with S3,
set `StorageFlow:S3:Enabled` to true and `StorageFlow:Minio:Enabled` to false.

## Run

```bash
dotnet run --project samples/StorageFlow.Sample.Api
```

Open:

```text
http://localhost:5078
```
