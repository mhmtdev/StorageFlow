using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Provider.S3;
using StorageFlow.Tests.Integration.Infrastructure;
using Testcontainers.LocalStack;

namespace StorageFlow.Tests.Integration.Fixtures;

public sealed class LocalStackFixture : IProviderFixture, IAsyncLifetime
{
    private const string AccessKey = "test";
    private const string SecretKey = "test";
    private readonly LocalStackContainer _container = new LocalStackBuilder(
            "localstack/localstack:4.14.0")
        .WithEnvironment("SERVICES", "s3")
        .Build();
    private ServiceProvider? _services;

    public string ProviderName => "s3";
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

    private static ServiceProvider BuildStorage(
        string endpoint,
        string accessKey,
        string secretKey) =>
        ProviderServiceFactory.Build(providers =>
            providers.UseS3(s3 => s3.Configure(config =>
            {
                config.Region = "us-east-1";
                config.AccessKey = accessKey;
                config.SecretKey = secretKey;
                config.ServiceUrl = endpoint;
            })).AsDefault());

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
