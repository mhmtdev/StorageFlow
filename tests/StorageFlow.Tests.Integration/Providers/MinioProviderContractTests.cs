using StorageFlow.Tests.Integration.Contracts;
using StorageFlow.Tests.Integration.Fixtures;
using StorageFlow.Tests.Integration.Infrastructure;

namespace StorageFlow.Tests.Integration.Providers;

[Collection(CollectionName)]
public sealed class MinioProviderContractTests(MinioFixture fixture)
    : ObjectStorageProviderContract(fixture)
{
    public const string CollectionName = "MinIO";

    [DockerFact]
    public Task InvalidCredentials_ReturnPermissionDenied() =>
        AssertInvalidCredentialsAreRejectedAsync();

    [DockerFact]
    public async Task Download_EarlyDisposeCancelsBoundedPipeProducer()
    {
        var bytes = new byte[4 * 1024 * 1024];
        Random.Shared.NextBytes(bytes);
        var upload = await fixture.Storage
            .FromStream(new MemoryStream(bytes), "large.bin", contentLength: bytes.Length)
            .UploadAsync(fixture.Bucket);
        Assert.True(upload.IsSuccess, upload.Error?.Message);

        var download = await fixture.Storage
            .Object(fixture.Bucket, upload.Value!.ObjectKey)
            .DownloadAsync();
        Assert.True(download.IsSuccess, download.Error?.Message);
        var buffer = new byte[1024];
        Assert.True(await download.Value!.Content.ReadAsync(buffer) > 0);

        await download.Value.Content.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.Storage.Object(fixture.Bucket, upload.Value.ObjectKey).DeleteAsync();
    }
}

[CollectionDefinition(MinioProviderContractTests.CollectionName)]
public sealed class MinioCollection : ICollectionFixture<MinioFixture>;
