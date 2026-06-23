using Amazon.S3;
using Amazon.S3.Model;
using Minio;
using NSubstitute;
using System.Net;
using StorageFlow.Abstractions.Exceptions;
using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Abstractions.Models;
using StorageFlow.Provider.Minio;
using StorageFlow.Provider.RustFs;
using StorageFlow.Provider.S3;

namespace StorageFlow.Tests.Unit.Providers;

public class ProviderMetadataTests
{
    [Fact]
    public async Task S3_UploadPassesMetadataHeadersAndNormalizedETag()
    {
        var client = Substitute.For<IAmazonS3>();
        PutObjectRequest? captured = null;
        client.PutObjectAsync(
                Arg.Do<PutObjectRequest>(request => captured = request),
                Arg.Any<CancellationToken>())
            .Returns(new PutObjectResponse { ETag = "\"s3-etag\"" });
        var provider = new S3StorageProvider(client);

        var result = await provider.UploadAsync(
            "bucket",
            "key",
            new MemoryStream([1]),
            contentLength: 1,
            metadata: new Dictionary<string, string> { ["source"] = "api" },
            headers: new UploadHeaders
            {
                CacheControl = "public, max-age=60",
                ContentDisposition = "attachment"
            });

        Assert.Equal("api", captured!.Metadata["source"]);
        Assert.Equal(1, captured.Headers.ContentLength);
        Assert.Equal("public, max-age=60", captured.Headers.CacheControl);
        Assert.Equal("attachment", captured.Headers.ContentDisposition);
        Assert.Equal("s3-etag", result.ETag);
    }

    [Fact]
    public async Task RustFs_UploadPassesMetadataHeadersAndNormalizedETag()
    {
        var client = Substitute.For<IAmazonS3>();
        PutObjectRequest? captured = null;
        client.PutObjectAsync(
                Arg.Do<PutObjectRequest>(request => captured = request),
                Arg.Any<CancellationToken>())
            .Returns(new PutObjectResponse { ETag = "\"rustfs-etag\"" });
        var provider = new RustFsStorageProvider(client);

        var result = await provider.UploadAsync(
            "bucket",
            "key",
            new MemoryStream([1]),
            contentLength: 1,
            metadata: new Dictionary<string, string> { ["source"] = "api" },
            headers: new UploadHeaders
            {
                CacheControl = "private, max-age=0",
                ContentDisposition = "inline"
            });

        Assert.Equal("api", captured!.Metadata["source"]);
        Assert.Equal(1, captured.Headers.ContentLength);
        Assert.Equal("private, max-age=0", captured.Headers.CacheControl);
        Assert.Equal("inline", captured.Headers.ContentDisposition);
        Assert.Equal("rustfs-etag", result.ETag);
    }

    [Fact]
    public async Task Minio_UploadPassesMetadataAndStandardHeaders()
    {
        var minioClient = Substitute.For<IMinioClient>();
        var uploadClient = Substitute.For<IAmazonS3>();
        PutObjectRequest? captured = null;
        uploadClient.PutObjectAsync(
                Arg.Do<PutObjectRequest>(request => captured = request),
                Arg.Any<CancellationToken>())
            .Returns(new PutObjectResponse { ETag = "\"minio-etag\"" });
        var provider = new MinioStorageProvider(minioClient, uploadClient);

        var result = await provider.UploadAsync(
            "bucket",
            "key",
            new MemoryStream([1]),
            contentLength: 1,
            metadata: new Dictionary<string, string> { ["source"] = "api" },
            headers: new UploadHeaders
            {
                CacheControl = "public, max-age=60",
                ContentDisposition = "inline"
            });

        Assert.Equal("api", captured!.Metadata["source"]);
        Assert.Equal(1, captured.Headers.ContentLength);
        Assert.Equal("public, max-age=60", captured.Headers.CacheControl);
        Assert.Equal("inline", captured.Headers.ContentDisposition);
        Assert.Equal("minio-etag", result.ETag);
    }

    [Theory]
    [InlineData("s3")]
    [InlineData("rustfs")]
    public async Task AwsCompatible_DownloadStreamsContentAndMapsObjectInfo(
        string providerName)
    {
        var client = Substitute.For<IAmazonS3>();
        var source = new TrackingStream([1, 2, 3, 4]);
        var modified = DateTime.UtcNow.AddMinutes(-5);
        var response = new GetObjectResponse
        {
            ResponseStream = source,
            ETag = "\"download-etag\"",
            LastModified = modified
        };
        response.Headers.ContentType = "image/png";
        response.Headers.ContentLength = 4;
        response.Metadata["x-amz-meta-source"] = "api";
        client.GetObjectAsync(
                Arg.Any<GetObjectRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(response);

        IStorageProvider provider = providerName == "s3"
            ? new S3StorageProvider(client)
            : new RustFsStorageProvider(client);

        var result = await provider.DownloadAsync("bucket", "key");

        Assert.Equal("image/png", result.ContentType);
        Assert.Equal(4, result.ContentLength);
        Assert.Equal("download-etag", result.ETag);
        Assert.Equal(modified, result.LastModified);
        Assert.Equal("api", result.Metadata["SOURCE"]);
        Assert.Throws<NotSupportedException>(
            () => ((IDictionary<string, string>)result.Metadata).Add("x", "y"));

        using var destination = new MemoryStream();
        await result.Content.CopyToAsync(destination);
        Assert.Equal([1, 2, 3, 4], destination.ToArray());
        Assert.False(source.IsDisposed);

        await result.Content.DisposeAsync();
        Assert.True(source.IsDisposed);
    }

    [Theory]
    [InlineData("s3", "NoSuchKey", HttpStatusCode.NotFound, StorageErrorCode.ObjectNotFound)]
    [InlineData("s3", "NoSuchBucket", HttpStatusCode.NotFound, StorageErrorCode.BucketNotFound)]
    [InlineData("s3", "AccessDenied", HttpStatusCode.Forbidden, StorageErrorCode.PermissionDenied)]
    [InlineData("s3", "InternalError", HttpStatusCode.InternalServerError, StorageErrorCode.ProviderError)]
    [InlineData("rustfs", "NoSuchKey", HttpStatusCode.NotFound, StorageErrorCode.ObjectNotFound)]
    [InlineData("rustfs", "NoSuchBucket", HttpStatusCode.NotFound, StorageErrorCode.BucketNotFound)]
    [InlineData("rustfs", "AccessDenied", HttpStatusCode.Forbidden, StorageErrorCode.PermissionDenied)]
    [InlineData("rustfs", "InternalError", HttpStatusCode.InternalServerError, StorageErrorCode.ProviderError)]
    public async Task AwsCompatible_DownloadMapsProviderErrors(
        string providerName,
        string sdkErrorCode,
        HttpStatusCode statusCode,
        StorageErrorCode expected)
    {
        var client = Substitute.For<IAmazonS3>();
        var failure = new AmazonS3Exception("failure")
        {
            ErrorCode = sdkErrorCode,
            StatusCode = statusCode
        };
        client.GetObjectAsync(
                Arg.Any<GetObjectRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GetObjectResponse>(failure));
        IStorageProvider provider = providerName == "s3"
            ? new S3StorageProvider(client)
            : new RustFsStorageProvider(client);

        var exception = await Assert.ThrowsAsync<StorageProviderException>(
            () => provider.DownloadAsync("bucket", "key"));

        Assert.Equal(expected, exception.ErrorCode);
    }

    [Theory]
    [InlineData("s3")]
    [InlineData("rustfs")]
    public async Task AwsCompatible_ExistsReturnsFalseForMissingObject(
        string providerName)
    {
        var client = Substitute.For<IAmazonS3>();
        var failure = new AmazonS3Exception("missing")
        {
            ErrorCode = "NoSuchKey",
            StatusCode = HttpStatusCode.NotFound
        };
        client.GetObjectMetadataAsync(
                Arg.Any<GetObjectMetadataRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GetObjectMetadataResponse>(failure));
        IStorageProvider provider = providerName == "s3"
            ? new S3StorageProvider(client)
            : new RustFsStorageProvider(client);

        var exists = await provider.ExistsAsync("bucket", "key");

        Assert.False(exists);
    }

    private sealed class TrackingStream(byte[] data) : MemoryStream(data)
    {
        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return base.DisposeAsync();
        }
    }
}
