using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Extension.Redis;
using StorageFlow.Provider.Minio;
using StorageFlow.Tests.Integration.Infrastructure;
using Testcontainers.Minio;

namespace StorageFlow.Tests.Integration.Fixtures;

public sealed class MinioFixture : IProviderFixture, IAsyncLifetime
{
    private const string AccessKey = "storageflow-minio";
    private const string SecretKey = "storageflow-minio-secret";
    private readonly MinioContainer _container = new MinioBuilder(
            "minio/minio:RELEASE.2025-09-07T16-13-09Z")
        .WithUsername(AccessKey)
        .WithPassword(SecretKey)
        .Build();
    private ServiceProvider? _services;

    public string ProviderName => "minio";
    public string Bucket { get; } = $"storageflow-{Guid.NewGuid():N}";
    public IStorageService Storage => ProviderServiceFactory.Storage(_services!);
    public IAmazonS3 ManagementClient { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        if (!DockerEnabled())
            return;

        await _container.StartAsync();
        var endpoint = _container.GetConnectionString();
        ManagementClient = CreateManagementClient(endpoint, AccessKey, SecretKey);
        await new S3BucketManager(ManagementClient).CreateWhenReadyAsync(Bucket);
        _services = BuildStorage(endpoint, AccessKey, SecretKey);
    }

    public async Task DisposeAsync()
    {
        if (_services is not null)
            await _services.DisposeAsync();
        ManagementClient?.Dispose();
        await _container.DisposeAsync();
    }

    public ServiceProvider CreateServicesWithCredentials(string accessKey, string secretKey) =>
        BuildStorage(
            _container.GetConnectionString(),
            accessKey,
            secretKey);

    public ServiceProvider CreateServicesWithRedis(
        string connectionString,
        string keyPrefix)
    {
        var endpoint = _container.GetConnectionString();
        var uri = new Uri(endpoint);
        return ProviderServiceFactory.Build(
            providers => providers.UseMinio(minio => minio.Configure(config =>
            {
                config.Endpoint = uri.Authority;
                config.AccessKey = AccessKey;
                config.SecretKey = SecretKey;
                config.UseSSL = false;
                config.Region = "us-east-1";
            })).AsDefault(),
            services => services.UseRedisPresignedUrlCache(options =>
            {
                options.ConnectionString = connectionString;
                options.KeyPrefix = keyPrefix;
            }));
    }

    private static ServiceProvider BuildStorage(
        string endpoint,
        string accessKey,
        string secretKey)
    {
        var uri = new Uri(endpoint);
        return ProviderServiceFactory.Build(providers =>
            providers.UseMinio(minio => minio.Configure(config =>
            {
                config.Endpoint = uri.Authority;
                config.AccessKey = accessKey;
                config.SecretKey = secretKey;
                config.UseSSL = uri.Scheme == Uri.UriSchemeHttps;
                config.Region = "us-east-1";
            })).AsDefault());
    }

    private static AmazonS3Client CreateManagementClient(
        string endpoint,
        string accessKey,
        string secretKey) =>
        new(new BasicAWSCredentials(accessKey, secretKey), new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true,
            AuthenticationRegion = "us-east-1"
        });

    private static bool DockerEnabled() =>
        string.Equals(
            Environment.GetEnvironmentVariable(DockerFactAttribute.EnabledVariable),
            "true",
            StringComparison.OrdinalIgnoreCase);
}
