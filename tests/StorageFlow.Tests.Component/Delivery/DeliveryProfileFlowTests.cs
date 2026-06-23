using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Tests.Component.Infrastructure;

namespace StorageFlow.Tests.Component.Delivery;

public class DeliveryProfileFlowTests
{
    private sealed class BlogDelivery : IDeliveryUrlPolicyKey;
    private sealed class BlogProfile : IStorageProfileKey;

    [Fact]
    public void Batch_DoesNotRequireStoredObjects()
    {
        using var services = ComponentTestHost.Build(options =>
        {
            options.DeliveryUrls.AddPolicy<BlogDelivery>(policy => policy
                .UseCdn("https://cdn.example.com")
                .WithPathPrefix("blog")
                .IncludeBucket());
            options.Profiles.Add<BlogProfile>(profile => profile.Bucket("media"));
        });

        var result = services.Storage()
            .Profile<BlogProfile>()
            .Objects(["missing-a.jpg", "missing-b.jpg"])
            .GetDeliveryUrls<BlogDelivery>();

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "https://cdn.example.com/blog/media/missing-a.jpg",
            result.Value![0].Url);
        Assert.Equal(
            "https://cdn.example.com/blog/media/missing-b.jpg",
            result.Value[1].Url);
    }
}
