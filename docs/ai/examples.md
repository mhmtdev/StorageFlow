# StorageFlow Agent Examples

Use these examples as application-level patterns. Keep policy keys and settings
owned by the application; do not expose framework configuration directly to
HTTP clients.

## Multiple providers

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

    options.Providers.UseS3(s3 => s3.Configure(config =>
    {
        config.Region = builder.Configuration["Storage:S3:Region"]!;
    }));
});
```

Default provider upload:

```csharp
var result = await storage
    .Validation<DocumentsPolicy>()
    .FromStream(stream, fileName, contentType, contentLength)
    .UploadAsync("documents", cancellationToken);
```

Explicit provider upload:

```csharp
var result = await storage
    .Provider(SFProvider.S3)
    .Validation<DocumentsPolicy>()
    .FromStream(stream, fileName, contentType, contentLength)
    .UploadAsync("documents", cancellationToken);
```

## Provider policy override

```csharp
options.Validation.AddPolicy<DocumentsPolicy>(policy =>
{
    policy.MaxFileSizeBytes = 10 * 1024 * 1024;
    policy.AllowedExtensions = [".pdf"];
    policy.AllowedMimeTypes = ["application/pdf"];
    policy.RequireValidSignature = true;
});

options.Providers.UseS3(s3 =>
{
    s3.Configure(config => config.Region = "eu-north-1");
    s3.Validation.AddPolicy<DocumentsPolicy>(policy =>
    {
        policy.MaxFileSizeBytes = 25 * 1024 * 1024;
        policy.AllowedExtensions = [".pdf"];
        policy.AllowedMimeTypes = ["application/pdf"];
        policy.RequireValidSignature = true;
    });
});
```

The S3 definition fully replaces the global `DocumentsPolicy` for S3
operations. It does not inherit individual fields.

## Streaming an HTTP download

```csharp
var result = await storage
    .Object(bucket, objectKey)
    .DownloadAsync(cancellationToken);

if (!result.IsSuccess)
    return MapStorageError(result.Error!);

var download = result.Value!;
httpContext.Response.RegisterForDisposeAsync(download.Content);

return Results.Stream(
    download.Content,
    contentType: download.ContentType ?? "application/octet-stream",
    fileDownloadName: Path.GetFileName(objectKey));
```

Do not dispose the stream before ASP.NET completes the response.

## Blog list delivery URLs

```csharp
var objectKeys = posts.Select(post => post.ImageObjectKey).ToArray();

var delivery = storage
    .Objects("blog-media", objectKeys)
    .GetDeliveryUrls<BlogImageDelivery>();

if (!delivery.IsSuccess)
    return MapStorageError(delivery.Error!);

var response = posts.Select((post, index) => new
{
    post.Id,
    post.Title,
    ImageUrl = delivery.Value![index].IsSuccess
        ? delivery.Value[index].Url
        : null
});
```

The database stores object keys, not expiring presigned URLs. Delivery URL
generation is suitable for public CDN-backed list pages.

## Presigned private download

```csharp
var result = await storage
    .Object(bucket, objectKey)
    .GetPresignedUrlAsync<PrivateDownloadPolicy>(cancellationToken);
```

Return the URL only after applying application authorization. Possession of a
valid presigned URL grants access until it expires.
