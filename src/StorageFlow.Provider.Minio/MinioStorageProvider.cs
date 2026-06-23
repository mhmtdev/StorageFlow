using Amazon.S3;
using Amazon.S3.Model;
using global::Minio;
using global::Minio.DataModel.Args;
using global::Minio.Exceptions;
using System.Collections.ObjectModel;
using System.IO.Pipelines;
using StorageFlow.Abstractions.Exceptions;
using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Abstractions.Models;

namespace StorageFlow.Provider.Minio;

/// <summary>
/// MinIO implementation of <see cref="IStorageProvider"/>.
/// Uses the MinIO SDK for object reads and presigned URLs, and an
/// S3-compatible client for uploads with standard object headers.
/// Provider SDK exceptions are re-thrown as <see cref="StorageProviderException"/>.
/// </summary>
public sealed class MinioStorageProvider : IStorageProvider
{
    private readonly IMinioClient _client;
    private readonly IAmazonS3 _uploadClient;

    /// <inheritdoc />
    public string ProviderName => "minio";

    /// <param name="client">A configured MinIO client instance.</param>
    /// <param name="uploadClient">
    /// An S3-compatible client used for uploads so standard object headers are
    /// handled correctly.
    /// </param>
    public MinioStorageProvider(IMinioClient client, IAmazonS3 uploadClient)
    {
        _client = client;
        _uploadClient = uploadClient;
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

            var response = await _uploadClient.PutObjectAsync(
                request,
                cancellationToken);

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
        catch (AmazonS3Exception ex)
        {
            throw ProviderFailure("MinIO upload", ex);
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
            var statArgs = new StatObjectArgs()
                .WithBucket(bucket)
                .WithObject(objectKey);
            var stat = await _client.StatObjectAsync(statArgs, cancellationToken);

            var pipe = new Pipe(new PipeOptions(
                pauseWriterThreshold: 256 * 1024,
                resumeWriterThreshold: 128 * 1024,
                useSynchronizationContext: false));
            var transferCancellation = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken);

            var args = new GetObjectArgs()
                .WithBucket(bucket)
                .WithObject(objectKey)
                .WithCallbackStream(async (stream, ct) =>
                {
                    await using var destination = pipe.Writer.AsStream(leaveOpen: true);
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                        ct,
                        transferCancellation.Token);
                    await stream.CopyToAsync(destination, linked.Token);
                });

            var transfer = TransferAsync(
                args,
                pipe.Writer,
                transferCancellation.Token);

            return new DownloadResult
            {
                Content = new MinioPipeStream(
                    pipe.Reader,
                    transfer,
                    transferCancellation),
                ContentType = stat.ContentType,
                ContentLength = stat.Size,
                ETag = NormalizeETag(stat.ETag),
                LastModified = stat.LastModified,
                Metadata = ReadMetadata(stat.MetaData)
            };
        }
        catch (AuthorizationException ex)
        {
            throw ProviderFailure("MinIO download", ex, bucket, objectKey);
        }
        catch (ObjectNotFoundException ex)
        {
            throw ProviderFailure("MinIO download", ex, bucket, objectKey);
        }
        catch (MinioException ex)
        {
            throw ProviderFailure("MinIO download", ex, bucket, objectKey);
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
            var args = new RemoveObjectArgs()
                .WithBucket(bucket)
                .WithObject(objectKey);

            await _client.RemoveObjectAsync(args, cancellationToken);
        }
        catch (AuthorizationException ex)
        {
            throw ProviderFailure("MinIO delete", ex, bucket, objectKey);
        }
        catch (MinioException ex)
        {
            throw ProviderFailure("MinIO delete", ex, bucket, objectKey);
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
            var args = new StatObjectArgs()
                .WithBucket(bucket)
                .WithObject(objectKey);

            await _client.StatObjectAsync(args, cancellationToken);
            return true;
        }
        catch (ObjectNotFoundException)
        {
            return false;
        }
        catch (BucketNotFoundException ex)
        {
            throw ProviderFailure("MinIO exists check", ex, bucket, objectKey);
        }
        catch (AuthorizationException ex)
        {
            throw ProviderFailure("MinIO exists check", ex, bucket, objectKey);
        }
        catch (MinioException ex)
        {
            throw ProviderFailure("MinIO exists check", ex, bucket, objectKey);
        }
    }

    /// <inheritdoc />
    public async Task<string> GetPresignedUrlAsync(
        string bucket,
        string objectKey,
        TimeSpan expiration,
        HttpMethod httpMethod,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (httpMethod == HttpMethod.Put)
            {
                var putArgs = new PresignedPutObjectArgs()
                    .WithBucket(bucket)
                    .WithObject(objectKey)
                    .WithExpiry((int)expiration.TotalSeconds);

                return await _client.PresignedPutObjectAsync(putArgs);
            }
            else
            {
                var getArgs = new PresignedGetObjectArgs()
                    .WithBucket(bucket)
                    .WithObject(objectKey)
                    .WithExpiry((int)expiration.TotalSeconds);

                return await _client.PresignedGetObjectAsync(getArgs);
            }
        }
        catch (AuthorizationException ex)
        {
            throw ProviderFailure(
                "MinIO presigned URL generation",
                ex,
                bucket,
                objectKey);
        }
        catch (MinioException ex)
        {
            throw ProviderFailure(
                "MinIO presigned URL generation",
                ex,
                bucket,
                objectKey);
        }
    }

    private async Task TransferAsync(
        GetObjectArgs args,
        PipeWriter writer,
        CancellationToken cancellationToken)
    {
        Exception? failure = null;
        try
        {
            await _client.GetObjectAsync(args, cancellationToken);
        }
        catch (Exception ex)
        {
            failure = ex is MinioException or AuthorizationException
                ? ProviderFailure("MinIO download", ex)
                : ex;
        }
        finally
        {
            await writer.CompleteAsync(failure);
        }
    }

    private StorageProviderException ProviderFailure(
        string operation,
        Exception exception,
        string? bucket = null,
        string? objectKey = null)
    {
        var code = exception switch
        {
            ObjectNotFoundException => StorageErrorCode.ObjectNotFound,
            BucketNotFoundException => StorageErrorCode.BucketNotFound,
            AuthorizationException or global::Minio.Exceptions.AccessDeniedException =>
                StorageErrorCode.PermissionDenied,
            _ => StorageErrorCode.ProviderError
        };
        var message = code == StorageErrorCode.ObjectNotFound
            ? $"Object '{objectKey}' not found in bucket '{bucket}'."
            : $"{operation} failed: {exception.Message}";
        return new StorageProviderException(
            message,
            ProviderName,
            exception,
            code);
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
        IDictionary<string, string>? metadata)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (metadata is null)
            return new ReadOnlyDictionary<string, string>(values);

        foreach (var (key, value) in metadata)
        {
            var normalizedKey = key.StartsWith("x-amz-meta-", StringComparison.OrdinalIgnoreCase)
                ? key["x-amz-meta-".Length..]
                : key;
            values[normalizedKey] = value;
        }

        return new ReadOnlyDictionary<string, string>(values);
    }

    private static string? NormalizeETag(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().Trim('"');
}
