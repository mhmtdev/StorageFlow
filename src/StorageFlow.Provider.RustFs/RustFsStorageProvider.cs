using Amazon.S3;
using Amazon.S3.Model;
using System.Collections.ObjectModel;
using StorageFlow.Abstractions.Exceptions;
using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Abstractions.Models;

namespace StorageFlow.Provider.RustFs;

/// <summary>
/// RustFS implementation of <see cref="IStorageProvider"/>.
/// Uses the AWS SDK for .NET (AWSSDK.S3) as recommended by the official RustFS documentation.
/// RustFS is fully S3-compatible, so the same SDK works seamlessly with a custom ServiceURL.
/// All SDK exceptions are caught and re-thrown as <see cref="StorageProviderException"/>.
/// </summary>
public sealed class RustFsStorageProvider : IStorageProvider
{
    private readonly IAmazonS3 _client;
    private readonly Protocol _presignedUrlProtocol;

    /// <inheritdoc />
    public string ProviderName => "rustfs";

    /// <param name="client">A configured <see cref="IAmazonS3"/> client pointing at the RustFS endpoint.</param>
    /// <param name="useHttpsForPresignedUrls">
    /// Whether generated presigned URLs use HTTPS. Disable only for an HTTP RustFS endpoint.
    /// </param>
    public RustFsStorageProvider(
        IAmazonS3 client,
        bool useHttpsForPresignedUrls = true)
    {
        _client = client;
        _presignedUrlProtocol = useHttpsForPresignedUrls
            ? Protocol.HTTPS
            : Protocol.HTTP;
    }

    /// <inheritdoc />
    public async Task<UploadResult> UploadAsync(
        string bucket,
        string objectKey,
        Stream content,
        string? contentType = null,
        long? contentLength = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        UploadHeaders? headers = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new PutObjectRequest
            {
                BucketName = bucket,
                Key = objectKey,
                InputStream = content,
                ContentType = contentType ?? "application/octet-stream",
                AutoCloseStream = false
            };
            if (contentLength.HasValue)
                request.Headers.ContentLength = contentLength.Value;
            request.Headers.CacheControl = headers?.CacheControl;
            request.Headers.ContentDisposition = headers?.ContentDisposition;

            if (metadata is not null)
            {
                foreach (var (key, value) in metadata)
                    request.Metadata[key] = value;
            }

            var response = await _client.PutObjectAsync(request, cancellationToken);

            return new UploadResult
            {
                ObjectKey = objectKey,
                Bucket = bucket,
                ProviderName = ProviderName,
                ContentType = contentType,
                ETag = NormalizeETag(response.ETag),
                SizeBytes = contentLength ?? (content.CanSeek ? content.Length : null),
                UploadedAt = DateTimeOffset.UtcNow
            };
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            throw ProviderFailure("RustFS upload", ex);
        }
        catch (AmazonS3Exception ex)
        {
            throw ProviderFailure("RustFS upload", ex);
        }
    }

    /// <inheritdoc />
    public async Task<DownloadResult> DownloadAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetObjectRequest { BucketName = bucket, Key = objectKey };
            var response = await _client.GetObjectAsync(request, cancellationToken);

            return new DownloadResult
            {
                Content = new RustFsResponseStream(response),
                ContentType = response.Headers.ContentType,
                ContentLength = response.Headers.ContentLength,
                ETag = NormalizeETag(response.ETag),
                LastModified = response.LastModified,
                Metadata = ReadMetadata(response.Metadata)
            };
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw ProviderFailure("RustFS download", ex, bucket, objectKey);
        }
        catch (AmazonS3Exception ex)
        {
            throw ProviderFailure("RustFS download", ex, bucket, objectKey);
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new DeleteObjectRequest { BucketName = bucket, Key = objectKey };
            await _client.DeleteObjectAsync(request, cancellationToken);
        }
        catch (AmazonS3Exception ex)
        {
            throw ProviderFailure("RustFS delete", ex, bucket, objectKey);
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetObjectMetadataRequest { BucketName = bucket, Key = objectKey };
            await _client.GetObjectMetadataAsync(request, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (AmazonS3Exception ex)
        {
            throw ProviderFailure("RustFS exists check", ex, bucket, objectKey);
        }
    }

    /// <inheritdoc />
    public Task<string> GetPresignedUrlAsync(
        string bucket,
        string objectKey,
        TimeSpan expiration,
        HttpMethod httpMethod,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var verb = httpMethod == HttpMethod.Put ? HttpVerb.PUT : HttpVerb.GET;

            var request = new GetPreSignedUrlRequest
            {
                BucketName = bucket,
                Key = objectKey,
                Verb = verb,
                Expires = DateTime.UtcNow.Add(expiration),
                Protocol = _presignedUrlProtocol
            };

            var url = _client.GetPreSignedURL(request);
            return Task.FromResult(url);
        }
        catch (AmazonS3Exception ex)
        {
            throw ProviderFailure("RustFS presigned URL generation", ex, bucket, objectKey);
        }
    }

    private StorageProviderException ProviderFailure(
        string operation,
        AmazonS3Exception exception,
        string? bucket = null,
        string? objectKey = null)
    {
        var code = MapErrorCode(exception);
        var message = code == StorageErrorCode.ObjectNotFound
            ? $"Object '{objectKey}' not found in bucket '{bucket}'."
            : $"{operation} failed: {exception.Message}";
        return new StorageProviderException(
            message,
            ProviderName,
            exception,
            code);
    }

    private static StorageErrorCode MapErrorCode(AmazonS3Exception exception)
    {
        if (exception.StatusCode is System.Net.HttpStatusCode.Unauthorized or
            System.Net.HttpStatusCode.Forbidden ||
            exception.ErrorCode is "AccessDenied" or "InvalidAccessKeyId" or
            "SignatureDoesNotMatch")
        {
            return StorageErrorCode.PermissionDenied;
        }

        if (exception.ErrorCode is "NoSuchBucket")
            return StorageErrorCode.BucketNotFound;

        if (exception.StatusCode == System.Net.HttpStatusCode.NotFound ||
            exception.ErrorCode is "NoSuchKey" or "NotFound")
        {
            return StorageErrorCode.ObjectNotFound;
        }

        return StorageErrorCode.ProviderError;
    }

    private static IReadOnlyDictionary<string, string> ReadMetadata(
        MetadataCollection metadata)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in metadata.Keys)
            values[NormalizeMetadataKey(key)] = metadata[key];
        return new ReadOnlyDictionary<string, string>(values);
    }

    private static string NormalizeMetadataKey(string key) =>
        key.StartsWith("x-amz-meta-", StringComparison.OrdinalIgnoreCase)
            ? key["x-amz-meta-".Length..]
            : key;

    private static string? NormalizeETag(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().Trim('"');
}
