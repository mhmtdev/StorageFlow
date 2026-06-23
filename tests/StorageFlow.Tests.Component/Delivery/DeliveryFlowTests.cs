using Microsoft.Extensions.DependencyInjection;
using StorageFlow.Abstractions.Exceptions;
using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Core.DependencyInjection;
using StorageFlow.Testing;

namespace StorageFlow.Tests.Component.Delivery;

public class DeliveryUrlTests
{
    private sealed class BlogDelivery : IDeliveryUrlPolicyKey;
    private sealed class UnknownDelivery : IDeliveryUrlPolicyKey;
    private sealed class BlogProfile : IStorageProfileKey;

    [Fact]
    public void GetDeliveryUrl_UsesGlobalPolicyAndEncodesPathSegments()
    {
        var storage = BuildService(options =>
            options.DeliveryUrls.AddPolicy<BlogDelivery>(policy => policy
                .UseCdn("https://cdn.example.com/")
                .WithPathPrefix("blog images")
                .IncludeBucket()));

        var result = storage
            .Object("media bucket", "2026/başlık görseli.jpg")
            .GetDeliveryUrl<BlogDelivery>();

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsSuccess);
        Assert.Equal(
            "https://cdn.example.com/blog%20images/media%20bucket/2026/ba%C5%9Fl%C4%B1k%20g%C3%B6rseli.jpg",
            result.Value.Url);
    }

    [Fact]
    public void GetDeliveryUrl_ProviderPolicyOverridesGlobalPolicy()
    {
        var storage = BuildService(
            options => options.DeliveryUrls.AddPolicy<BlogDelivery>(
                policy => policy.UseCdn("https://global.example.com")),
            provider => provider.DeliveryUrls.AddPolicy<BlogDelivery>(
                policy => policy.UseCdn("https://provider.example.com")));

        var result = storage
            .Provider(SFTestProvider.InMemory)
            .Object("media", "cover.jpg")
            .GetDeliveryUrl<BlogDelivery>();

        Assert.Equal("https://provider.example.com/cover.jpg", result.Value!.Url);
    }

    [Fact]
    public void GetDeliveryUrls_PreservesOrderDuplicatesAndItemFailures()
    {
        var storage = BuildService(options =>
            options.DeliveryUrls.AddPolicy<BlogDelivery>(
                policy => policy.UseCdn("https://cdn.example.com")));
        string[] keys = ["a.jpg", "../secret.jpg", "a.jpg", "folder/b.jpg"];

        var result = storage
            .Objects("media", keys)
            .GetDeliveryUrls<BlogDelivery>();

        Assert.True(result.IsSuccess);
        Assert.Equal(keys.Length, result.Value!.Count);
        Assert.Equal("a.jpg", result.Value[0].ObjectKey);
        Assert.Equal(result.Value[0].Url, result.Value[2].Url);
        Assert.False(result.Value[1].IsSuccess);
        Assert.Null(result.Value[1].Url);
        Assert.Equal(
            "https://cdn.example.com/folder/b.jpg",
            result.Value[3].Url);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10000)]
    public void GetDeliveryUrls_HandlesLargeBatchesSynchronously(int count)
    {
        var storage = BuildService(options =>
            options.DeliveryUrls.AddPolicy<BlogDelivery>(
                policy => policy.UseCdn("https://cdn.example.com")));
        var keys = new string[count];
        for (var index = 0; index < keys.Length; index++)
            keys[index] = $"images/{index}.jpg";

        var result = storage
            .Objects("media", keys)
            .GetDeliveryUrls<BlogDelivery>();

        Assert.True(result.IsSuccess);
        Assert.Equal(count, result.Value!.Count);
        Assert.Equal(
            $"https://cdn.example.com/images/{count - 1}.jpg",
            result.Value[count - 1].Url);
    }

    [Fact]
    public void GetDeliveryUrls_ProfileProvidesProviderAndBucket()
    {
        var storage = BuildService(options =>
        {
            options.DeliveryUrls.AddPolicy<BlogDelivery>(policy => policy
                .UseCdn("https://cdn.example.com")
                .IncludeBucket());
            options.Profiles.Add<BlogProfile>(profile => profile
                .Provider(SFTestProvider.InMemory)
                .Bucket("blogs"));
        });

        var result = storage
            .Profile<BlogProfile>()
            .Objects(["cover.jpg"])
            .GetDeliveryUrls<BlogDelivery>();

        Assert.Equal(
            "https://cdn.example.com/blogs/cover.jpg",
            result.Value![0].Url);
    }

    [Fact]
    public void GetDeliveryUrl_UnknownPolicyReturnsOuterFailure()
    {
        var storage = BuildService(options =>
            options.DeliveryUrls.AddPolicy<BlogDelivery>(
                policy => policy.UseCdn("https://cdn.example.com")));

        var result = storage
            .Object("media", "cover.jpg")
            .GetDeliveryUrl<UnknownDelivery>();

        Assert.False(result.IsSuccess);
        Assert.Contains("Delivery URL policy", result.Error!.Message);
    }

    [Fact]
    public void DeliveryMethods_AreSynchronous()
    {
        var single = typeof(IStorageObjectBuilder)
            .GetMethod(nameof(IStorageObjectBuilder.GetDeliveryUrl));
        var batch = typeof(IStorageObjectCollectionBuilder)
            .GetMethod(nameof(IStorageObjectCollectionBuilder.GetDeliveryUrls));

        Assert.NotNull(single);
        Assert.NotNull(batch);
        Assert.False(typeof(Task).IsAssignableFrom(single!.ReturnType));
        Assert.False(typeof(Task).IsAssignableFrom(batch!.ReturnType));
    }

    [Theory]
    [InlineData("https://cdn.example.com")]
    [InlineData("http://localhost:8080")]
    [InlineData("http://127.0.0.1:8080")]
    [InlineData("http://[::1]:8080")]
    public void Policy_AllowsHttpsAndLocalHttp(string baseUrl)
    {
        var storage = BuildService(options =>
            options.DeliveryUrls.AddPolicy<BlogDelivery>(
                policy => policy.UseCdn(baseUrl)));

        var result = storage
            .Object("media", "cover.jpg")
            .GetDeliveryUrl<BlogDelivery>();

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("http://cdn.example.com")]
    [InlineData("https://cdn.example.com?token=value")]
    [InlineData("https://cdn.example.com#fragment")]
    [InlineData("not-a-url")]
    public void Policy_RejectsInvalidBaseUrl(string baseUrl)
    {
        Assert.Throws<StorageConfigurationException>(() =>
            BuildService(options =>
                options.DeliveryUrls.AddPolicy<BlogDelivery>(
                    policy => policy.UseCdn(baseUrl))));
    }

    [Theory]
    [InlineData("../assets")]
    [InlineData("assets\\images")]
    [InlineData("assets/./images")]
    public void Policy_RejectsInvalidPrefix(string prefix)
    {
        Assert.Throws<StorageConfigurationException>(() =>
            BuildService(options =>
                options.DeliveryUrls.AddPolicy<BlogDelivery>(policy => policy
                    .UseCdn("https://cdn.example.com")
                    .WithPathPrefix(prefix))));
    }

    [Fact]
    public void RegisteredPolicy_CannotBeMutated()
    {
        StorageFlow.Abstractions.Models.DeliveryUrlPolicy? captured = null;

        BuildService(options =>
            options.DeliveryUrls.AddPolicy<BlogDelivery>(policy =>
            {
                captured = policy;
                policy.UseCdn("https://cdn.example.com");
            }));

        Assert.Throws<StorageConfigurationException>(() =>
            captured!.WithPathPrefix("changed"));
    }

    private static IStorageService BuildService(
        Action<StorageFlow.Core.Configuration.StorageFlowOptions>? configure = null,
        Action<InMemoryRegistrationBuilder>? configureProvider = null)
    {
        var services = new ServiceCollection();
        services.AddStorageFlow(options =>
        {
            options.Providers.UseInMemory(configureProvider);
            configure?.Invoke(options);
        });

        return services.BuildServiceProvider()
            .GetRequiredService<IStorageService>();
    }
}
