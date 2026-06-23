using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Tests.Component.Infrastructure;

namespace StorageFlow.Tests.Component.Profiles;

public class ProfileFlowTests
{
    private sealed class DownloadPolicy : IPresignedUrlPolicyKey;
    private sealed class AlternatePolicy : IPresignedUrlPolicyKey;
    private sealed class MediaProfile : IStorageProfileKey;

    [Fact]
    public async Task Profile_ProvidesBucketAndPresignedPolicy()
    {
        await using var services = ComponentTestHost.Build(options =>
        {
            options.PresignedUrls.AddPolicy<DownloadPolicy>(policy =>
                policy.Expiration = TimeSpan.FromMinutes(30));
            options.Profiles.Add<MediaProfile>(profile => profile
                .Bucket("media")
                .PresignedUrl<DownloadPolicy>());
        });
        var storage = services.Storage();
        var upload = await storage
            .FromStream(new MemoryStream([1, 2, 3]), "photo.jpg")
            .UploadAsync("media");

        var url = await storage
            .Profile<MediaProfile>()
            .Object(upload.Value!.ObjectKey)
            .GetPresignedUrlAsync();

        Assert.True(url.IsSuccess);
        Assert.Contains("memory.local", url.Value);
    }

    [Fact]
    public async Task GenericPresignedPolicy_OverridesProfileSelection()
    {
        await using var services = ComponentTestHost.Build(options =>
        {
            options.PresignedUrls.AddPolicy<DownloadPolicy>(_ => { });
            options.PresignedUrls.AddPolicy<AlternatePolicy>(_ => { });
            options.Profiles.Add<MediaProfile>(profile => profile
                .Bucket("media")
                .PresignedUrl<DownloadPolicy>());
        });
        var storage = services.Storage();
        var upload = await storage
            .FromStream(new MemoryStream([1]), "photo.jpg")
            .UploadAsync("media");

        var url = await storage
            .Profile<MediaProfile>()
            .Object(upload.Value!.ObjectKey)
            .GetPresignedUrlAsync<AlternatePolicy>();

        Assert.True(url.IsSuccess);
    }
}
