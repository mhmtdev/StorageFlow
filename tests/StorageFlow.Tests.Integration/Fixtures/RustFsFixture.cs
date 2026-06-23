using Amazon.Runtime;
using Amazon.S3;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.DependencyInjection;
using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Provider.RustFs;
using StorageFlow.Tests.Integration.Infrastructure;

namespace StorageFlow.Tests.Integration.Fixtures;

public sealed class RustFsFixture : IProviderFixture, IAsyncLifetime
{
    private const string AccessKey = "rustfsadmin";
    private const string SecretKey = "rustfsadmin";
    private const ushort ApiPort = 9000;
    private readonly IContainer _container = new ContainerBuilder(
            "rustfs/rustfs:1.0.0-beta.7")
        .WithPortBinding(ApiPort, true)
        .WithEnvironment("RUSTFS_ACCESS_KEY", AccessKey)
        .WithEnvironment("RUSTFS_SECRET_KEY", SecretKey)
        .WithCommand(
            "--access-key", AccessKey,
            "--secret-key", SecretKey,
            "/data")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(ApiPort))
        .Build();
    private ServiceProvider? _services;

    public string ProviderName => "rustfs";
    public string Bucket { get; } = $"storageflow-{Guid.NewGuid():N}";
    public IStorageService Storage => ProviderServiceFactory.Storage(_services!);
    public IAmazonS3 ManagementClient { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        if (!DockerEnabled())
            return;

        await _container.StartAsync();
        var endpoint = Endpoint();
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
        BuildStorage(Endpoint(), accessKey, secretKey);

    private string Endpoint() =>
        $"http://{_container.Hostname}:{_container.GetMappedPublicPort(ApiPort)}";

    private static ServiceProvider BuildStorage(
        string endpoint,
        string accessKey,
        string secretKey) =>
        ProviderServiceFactory.Build(providers =>
            providers.UseRustFs(rustFs => rustFs.Configure(config =>
            {
                config.ServiceUrl = endpoint;
                config.AccessKey = accessKey;
                config.SecretKey = secretKey;
                config.Region = "us-east-1";
                config.ForcePathStyle = true;
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
