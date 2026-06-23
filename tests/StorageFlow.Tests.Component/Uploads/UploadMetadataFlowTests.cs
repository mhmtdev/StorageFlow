using Microsoft.Extensions.DependencyInjection;
using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Testing;
using StorageFlow.Tests.Component.Infrastructure;

namespace StorageFlow.Tests.Component.Uploads;

public class UploadMetadataFlowTests
{
    [Fact]
    public async Task Metadata_LastValueWinsAndRoundTripsCaseInsensitively()
    {
        await using var services = ComponentTestHost.Build();
        var storage = services.Storage();
        var upload = await storage
            .Metadata("source", "first")
            .Metadata(new Dictionary<string, string>
            {
                ["source"] = "api",
                ["tenant"] = "alpha"
            })
            .FromStream(new MemoryStream([1, 2, 3]), "photo.jpg")
            .UploadAsync("media");

        var registry = services.GetRequiredService<IStorageProviderRegistry>();
        var provider = Assert.IsType<InMemoryStorageProvider>(
            registry.Get(SFTestProvider.InMemory));
        var metadata = provider.GetMetadata("media", upload.Value!.ObjectKey);

        Assert.Equal("api", metadata!["SOURCE"]);
        Assert.Equal("alpha", metadata["tenant"]);
    }

    [Fact]
    public async Task HeadersAndDeterministicETag_RoundTripSeparatelyFromMetadata()
    {
        await using var services = ComponentTestHost.Build();
        var storage = services.Storage();
        byte[] bytes = [1, 2, 3];
        var upload = await storage
            .CacheControl("public, max-age=60")
            .ContentDisposition("inline")
            .Metadata("source", "api")
            .FromStream(new MemoryStream(bytes), "photo.jpg", "image/jpeg", bytes.Length)
            .UploadAsync("media");

        var registry = services.GetRequiredService<IStorageProviderRegistry>();
        var provider = Assert.IsType<InMemoryStorageProvider>(
            registry.Get(SFTestProvider.InMemory));
        var headers = provider.GetHeaders("media", upload.Value!.ObjectKey);
        var download = await storage.Object("media", upload.Value.ObjectKey).DownloadAsync();

        Assert.Equal("public, max-age=60", headers!.CacheControl);
        Assert.Equal("inline", headers.ContentDisposition);
        Assert.Equal(upload.Value.ETag, download.Value!.ETag);
        Assert.Equal("api", download.Value.Metadata["SOURCE"]);
        await download.Value.Content.DisposeAsync();
    }
}
