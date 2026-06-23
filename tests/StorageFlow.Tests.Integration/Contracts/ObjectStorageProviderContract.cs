using Amazon.S3.Model;
using StorageFlow.Abstractions.Models;
using StorageFlow.Tests.Integration.Infrastructure;

namespace StorageFlow.Tests.Integration.Contracts;

public abstract class ObjectStorageProviderContract(IProviderFixture fixture)
{
    protected IProviderFixture Fixture { get; } = fixture;

    [DockerFact]
    public async Task FullLifecycle_RoundTripsContentMetadataHeadersAndPresignedGet()
    {
        byte[] bytes = [0x25, 0x50, 0x44, 0x46, 1, 2, 3, 4, 5];
        var upload = await Fixture.Storage
            .CacheControl("private, max-age=0")
            .ContentDisposition("attachment")
            .Metadata("source", "integration")
            .FromStream(new MemoryStream(bytes), "report.pdf", "application/pdf", bytes.Length)
            .UploadAsync(Fixture.Bucket);

        Assert.True(upload.IsSuccess, upload.Error?.Message);
        Assert.Equal(Fixture.ProviderName, upload.Value!.ProviderName);
        Assert.False(string.IsNullOrWhiteSpace(upload.Value.ETag));
        Assert.DoesNotContain('"', upload.Value.ETag!);

        var exists = await Fixture.Storage
            .Object(Fixture.Bucket, upload.Value.ObjectKey)
            .ExistsAsync();
        var download = await Fixture.Storage
            .Object(Fixture.Bucket, upload.Value.ObjectKey)
            .DownloadAsync();

        Assert.True(exists.Value);
        Assert.True(download.IsSuccess, download.Error?.Message);
        Assert.Equal("application/pdf", download.Value!.ContentType);
        Assert.Equal(bytes.Length, download.Value.ContentLength);
        Assert.Equal("integration", download.Value.Metadata["SOURCE"]);
        Assert.False(string.IsNullOrWhiteSpace(download.Value.ETag));
        Assert.NotNull(download.Value.LastModified);
        await using (var content = download.Value.Content)
        {
            using var copy = new MemoryStream();
            await content.CopyToAsync(copy);
            Assert.Equal(bytes, copy.ToArray());
        }

        var metadata = await Fixture.ManagementClient.GetObjectMetadataAsync(
            new GetObjectMetadataRequest
            {
                BucketName = Fixture.Bucket,
                Key = upload.Value.ObjectKey
            });
        Assert.Equal(
            ["max-age=0", "private"],
            metadata.Headers.CacheControl
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Order(StringComparer.Ordinal));
        Assert.Equal("attachment", metadata.Headers.ContentDisposition);

        var presigned = await Fixture.Storage
            .Object(Fixture.Bucket, upload.Value.ObjectKey)
            .GetPresignedUrlAsync<IntegrationDownloadPolicy>();
        Assert.True(presigned.IsSuccess, presigned.Error?.Message);
        Assert.StartsWith("http://", presigned.Value, StringComparison.OrdinalIgnoreCase);
        using var http = new HttpClient();
        Assert.Equal(bytes, await http.GetByteArrayAsync(presigned.Value));

        var delete = await Fixture.Storage
            .Object(Fixture.Bucket, upload.Value.ObjectKey)
            .DeleteAsync();
        var secondDelete = await Fixture.Storage
            .Object(Fixture.Bucket, upload.Value.ObjectKey)
            .DeleteAsync();
        var missing = await Fixture.Storage
            .Object(Fixture.Bucket, upload.Value.ObjectKey)
            .ExistsAsync();
        Assert.True(delete.IsSuccess, delete.Error?.Message);
        Assert.True(secondDelete.IsSuccess, secondDelete.Error?.Message);
        Assert.False(missing.Value);
    }

    [DockerFact]
    public async Task MissingObjectAndBucket_MapCommonErrorCodes()
    {
        var missingObject = await Fixture.Storage
            .Object(Fixture.Bucket, $"missing-{Guid.NewGuid():N}.bin")
            .DownloadAsync();
        var missingBucket = await Fixture.Storage
            .Object($"missing-{Guid.NewGuid():N}", "object.bin")
            .DownloadAsync();

        Assert.False(missingObject.IsSuccess);
        Assert.Equal(StorageErrorCode.ObjectNotFound, missingObject.Error!.Code);
        Assert.False(missingBucket.IsSuccess);
        Assert.Equal(StorageErrorCode.BucketNotFound, missingBucket.Error!.Code);
    }

    [DockerFact]
    public async Task LargeNonSeekableUpload_PreservesBytesAndCallerOwnership()
    {
        var bytes = new byte[2 * 1024 * 1024];
        Random.Shared.NextBytes(bytes);
        await using var source = new NonSeekableTrackingStream(bytes);

        var upload = await Fixture.Storage
            .FromStream(source, "large.bin", "application/octet-stream", bytes.Length)
            .UploadAsync(Fixture.Bucket);

        Assert.True(upload.IsSuccess, upload.Error?.Message);
        Assert.False(source.IsDisposed);
        var download = await Fixture.Storage
            .Object(Fixture.Bucket, upload.Value!.ObjectKey)
            .DownloadAsync();
        Assert.True(download.IsSuccess, download.Error?.Message);
        await using var content = download.Value!.Content;
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy);
        Assert.Equal(bytes, copy.ToArray());
        await Fixture.Storage.Object(Fixture.Bucket, upload.Value.ObjectKey).DeleteAsync();
    }

    protected async Task AssertInvalidCredentialsAreRejectedAsync()
    {
        await using var services = Fixture.CreateServicesWithCredentials(
            "invalid-access",
            "invalid-secret");
        var storage = ProviderServiceFactory.Storage(services);

        var result = await storage
            .FromStream(new MemoryStream([1, 2, 3]), "denied.bin")
            .UploadAsync(Fixture.Bucket);

        Assert.False(result.IsSuccess);
        Assert.Equal(StorageErrorCode.PermissionDenied, result.Error!.Code);
    }

    private sealed class NonSeekableTrackingStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream _inner = new(bytes);

        public bool IsDisposed { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            _inner.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
