using StorageFlow.Abstractions.Models;
using StorageFlow.Tests.Component.Infrastructure;

namespace StorageFlow.Tests.Component.Objects;

public class ObjectCrudTests
{
    [Fact]
    public async Task FullCrudCycle_RoundTripsBytesAndObjectInformation()
    {
        await using var services = ComponentTestHost.Build();
        var storage = services.Storage();
        byte[] original = [1, 2, 3, 4, 5];

        var upload = await storage
            .FromStream(new MemoryStream(original), "photo.jpg", "image/jpeg", original.Length)
            .UploadAsync("media");
        var exists = await storage.Object("media", upload.Value!.ObjectKey).ExistsAsync();
        var download = await storage.Object("media", upload.Value.ObjectKey).DownloadAsync();

        Assert.True(upload.IsSuccess);
        Assert.True(exists.Value);
        Assert.True(download.IsSuccess);
        Assert.Equal("image/jpeg", download.Value!.ContentType);
        Assert.Equal(original.Length, download.Value.ContentLength);
        Assert.Equal(upload.Value.ETag, download.Value.ETag);
        Assert.NotNull(download.Value.LastModified);

        await using var content = download.Value.Content;
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy);
        Assert.Equal(original, copy.ToArray());

        var delete = await storage.Object("media", upload.Value.ObjectKey).DeleteAsync();
        var missing = await storage.Object("media", upload.Value.ObjectKey).ExistsAsync();
        Assert.True(delete.IsSuccess);
        Assert.False(missing.Value);
    }

    [Fact]
    public async Task Download_MissingObjectReturnsObjectNotFound()
    {
        await using var services = ComponentTestHost.Build();

        var result = await services.Storage()
            .Object("media", "missing.bin")
            .DownloadAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(StorageErrorCode.ObjectNotFound, result.Error!.Code);
    }
}
