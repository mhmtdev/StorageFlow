using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StorageFlow.Abstractions.Exceptions;
using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Abstractions.Models;
using StorageFlow.Core.DependencyInjection;
using StorageFlow.Core.Registry;
using StorageFlow.Provider.Minio;
using StorageFlow.Provider.RustFs;
using StorageFlow.Provider.S3;
using StorageFlow.Testing;

namespace StorageFlow.Tests.Component.Providers;

public class StorageProviderRegistryTests
{
    private static IStorageProvider MakeProvider(string name)
    {
        var mock = Substitute.For<IStorageProvider>();
        mock.ProviderName.Returns(name);
        return mock;
    }

    [Fact]
    public void Get_ByToken_ReturnsCorrectProvider()
    {
        var providers = new Dictionary<StorageProviderToken, IStorageProvider>
        {
            [SFProvider.Minio] = MakeProvider("minio")
        };
        var registry = new StorageProviderRegistry(providers, SFProvider.Minio);

        var provider = registry.Get(SFProvider.Minio);

        Assert.Equal("minio", provider.ProviderName);
    }

    [Fact]
    public void Get_WhenProviderNotFound_ThrowsStorageConfigurationException()
    {
        var registry = new StorageProviderRegistry(
            new Dictionary<StorageProviderToken, IStorageProvider>(),
            null);

        Assert.Throws<StorageConfigurationException>(() => registry.Get(SFProvider.S3));
    }

    [Fact]
    public void GetDefault_WhenSingleProvider_ReturnsThatProvider()
    {
        var providers = new Dictionary<StorageProviderToken, IStorageProvider>
        {
            [SFProvider.Minio] = MakeProvider("minio")
        };
        var registry = new StorageProviderRegistry(providers, null);

        var provider = registry.GetDefault();

        Assert.Equal("minio", provider.ProviderName);
        Assert.Equal(SFProvider.Minio, registry.GetDefaultProviderToken());
    }

    [Fact]
    public void GetDefault_WhenDefaultIsSet_ReturnsThatProvider()
    {
        var providers = new Dictionary<StorageProviderToken, IStorageProvider>
        {
            [SFProvider.Minio] = MakeProvider("minio"),
            [SFProvider.S3] = MakeProvider("s3")
        };
        var registry = new StorageProviderRegistry(providers, SFProvider.S3);

        var provider = registry.GetDefault();

        Assert.Equal("s3", provider.ProviderName);
    }

    [Fact]
    public void GetDefault_WhenMultipleProvidersAndNoDefault_ThrowsStorageConfigurationException()
    {
        var providers = new Dictionary<StorageProviderToken, IStorageProvider>
        {
            [SFProvider.Minio] = MakeProvider("minio"),
            [SFProvider.S3] = MakeProvider("s3")
        };
        var registry = new StorageProviderRegistry(providers, null);

        Assert.Throws<StorageConfigurationException>(() => registry.GetDefault());
    }

    [Fact]
    public void GetRegisteredProviders_ReturnsAllTokens()
    {
        var providers = new Dictionary<StorageProviderToken, IStorageProvider>
        {
            [SFProvider.Minio] = MakeProvider("minio"),
            [SFProvider.S3] = MakeProvider("s3")
        };
        var registry = new StorageProviderRegistry(providers, SFProvider.Minio);

        var tokens = registry.GetRegisteredProviders().ToList();

        Assert.Contains(SFProvider.Minio, tokens);
        Assert.Contains(SFProvider.S3, tokens);
    }
}

public class ProviderRegistrationTests
{
    private sealed class DownloadPolicy : IPresignedUrlPolicyKey;

    [Fact]
    public void StorageProviderToken_HasNoPublicConstructor()
    {
        Assert.Empty(typeof(StorageProviderToken).GetConstructors());
    }

    [Fact]
    public void StorageService_ExposesTokenProviderSelectorOnly()
    {
        var methods = typeof(IStorageOperationBuilder).GetMethods();

        Assert.Contains(methods, method =>
            method.Name == nameof(IStorageService.Provider) &&
            method.GetParameters() is [{ ParameterType: var parameterType }] &&
            parameterType == typeof(StorageProviderToken));
        Assert.DoesNotContain(methods, method => method.Name == "For");
    }

    [Fact]
    public void UseInMemory_WhenRegisteredTwice_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<StorageConfigurationException>(() =>
            services.AddStorageFlow(options =>
            {
                options.Providers.UseInMemory();
                options.Providers.UseInMemory();
            }));
    }

    [Fact]
    public void AsDefault_WhenCalledForMultipleProviders_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<StorageConfigurationException>(() =>
            services.AddStorageFlow(options =>
            {
                options.Providers.UseInMemory().AsDefault();
                options.Providers.UseMismatchedInMemory().AsDefault();
            }));
    }

    [Fact]
    public void UseMinio_WhenConfigureIsMissing_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<StorageConfigurationException>(() =>
            services.AddStorageFlow(options =>
                options.Providers.UseMinio(_ => { })));
    }

    [Fact]
    public void UseMinio_WhenConfigureIsCalledTwice_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<StorageConfigurationException>(() =>
            services.AddStorageFlow(options =>
                options.Providers.UseMinio(minio =>
                {
                    minio.Configure(config =>
                    {
                        config.Endpoint = "localhost:9000";
                        config.AccessKey = "a";
                        config.SecretKey = "b";
                    });
                    minio.Configure(config =>
                    {
                        config.Endpoint = "localhost:9000";
                        config.AccessKey = "a";
                        config.SecretKey = "b";
                    });
                })));
    }

    [Theory]
    [InlineData("access", null)]
    [InlineData(null, "secret")]
    public void UseS3_WhenOnlyOneStaticCredentialIsSet_Throws(
        string? accessKey,
        string? secretKey)
    {
        var services = new ServiceCollection();

        Assert.Throws<StorageConfigurationException>(() =>
            services.AddStorageFlow(options =>
                options.Providers.UseS3(s3 => s3.Configure(config =>
                {
                    config.Region = "eu-central-1";
                    config.AccessKey = accessKey;
                    config.SecretKey = secretKey;
                }))));
    }

    [Fact]
    public void UseS3_WhenSessionTokenHasNoStaticCredentials_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<StorageConfigurationException>(() =>
            services.AddStorageFlow(options =>
                options.Providers.UseS3(s3 => s3.Configure(config =>
                {
                    config.Region = "eu-central-1";
                    config.SessionToken = "session-token";
                }))));
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("access", "secret", null)]
    [InlineData("access", "secret", "session")]
    public void UseS3_SupportsDefaultStaticAndSessionCredentialModes(
        string? accessKey,
        string? secretKey,
        string? sessionToken)
    {
        var services = new ServiceCollection();
        services.AddStorageFlow(options =>
            options.Providers.UseS3(s3 => s3.Configure(config =>
            {
                config.Region = "eu-central-1";
                config.AccessKey = accessKey;
                config.SecretKey = secretKey;
                config.SessionToken = sessionToken;
            })));
        using var provider = services.BuildServiceProvider();

        var registry = provider.GetRequiredService<IStorageProviderRegistry>();

        Assert.Equal("s3", registry.Get(SFProvider.S3).ProviderName);
    }

    [Theory]
    [InlineData("s3", "http://localhost:4566", "http://")]
    [InlineData("s3", "https://s3.example.com", "https://")]
    [InlineData("rustfs", "http://localhost:9000", "http://")]
    [InlineData("rustfs", "https://rustfs.example.com", "https://")]
    public async Task AwsCompatibleProvider_PresignedUrlUsesConfiguredEndpointScheme(
        string providerName,
        string serviceUrl,
        string expectedScheme)
    {
        var services = new ServiceCollection();
        services.AddStorageFlow(options =>
        {
            options.PresignedUrls.AddPolicy<DownloadPolicy>(policy =>
                policy.Expiration = TimeSpan.FromMinutes(5));

            if (providerName == "s3")
            {
                options.Providers.UseS3(s3 => s3.Configure(config =>
                {
                    config.Region = "us-east-1";
                    config.AccessKey = "access";
                    config.SecretKey = "secret";
                    config.ServiceUrl = serviceUrl;
                }));
            }
            else
            {
                options.Providers.UseRustFs(rustFs => rustFs.Configure(config =>
                {
                    config.Region = "us-east-1";
                    config.AccessKey = "access";
                    config.SecretKey = "secret";
                    config.ServiceUrl = serviceUrl;
                }));
            }
        });
        await using var provider = services.BuildServiceProvider();
        var storage = provider.GetRequiredService<IStorageService>();

        var result = await storage
            .Object("media", "folder/file.jpg")
            .GetPresignedUrlAsync<DownloadPolicy>();

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.StartsWith(expectedScheme, result.Value, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("s3")]
    [InlineData("rustfs")]
    public void AwsCompatibleProvider_InvalidServiceUrlThrows(string providerName)
    {
        var services = new ServiceCollection();

        Assert.Throws<StorageConfigurationException>(() =>
            services.AddStorageFlow(options =>
            {
                if (providerName == "s3")
                {
                    options.Providers.UseS3(s3 => s3.Configure(config =>
                    {
                        config.Region = "us-east-1";
                        config.ServiceUrl = "ftp://storage.example.com";
                    }));
                }
                else
                {
                    options.Providers.UseRustFs(rustFs => rustFs.Configure(config =>
                    {
                        config.AccessKey = "access";
                        config.SecretKey = "secret";
                        config.ServiceUrl = "ftp://storage.example.com";
                    }));
                }
            }));
    }

    [Fact]
    public void ProviderNameMismatch_WhenRegistryIsResolved_Throws()
    {
        var services = new ServiceCollection();
        services.AddStorageFlow(options =>
            options.Providers.UseMismatchedInMemory());
        using var provider = services.BuildServiceProvider();

        Assert.Throws<StorageConfigurationException>(
            () => provider.GetRequiredService<IStorageProviderRegistry>());
    }

    [Fact]
    public async Task Provider_UnregisteredToken_ReturnsFailure()
    {
        var services = new ServiceCollection();
        services.AddStorageFlow(options =>
            options.Providers.UseInMemory());
        await using var provider = services.BuildServiceProvider();
        var storage = provider.GetRequiredService<IStorageService>();

        var result = await storage
            .Provider(SFProvider.Minio)
            .FromStream(new MemoryStream([1, 2, 3]), "file.bin")
            .UploadAsync("bucket");

        Assert.False(result.IsSuccess);
        Assert.Contains("not registered", result.Error!.Message);
    }
}
