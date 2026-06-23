using StorageFlow.Tests.Integration.Infrastructure;
using Testcontainers.Redis;

namespace StorageFlow.Tests.Integration.Fixtures;

public sealed class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder("redis:7.0").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        if (!DockerEnabled())
            return;

        await _container.StartAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    private static bool DockerEnabled() =>
        string.Equals(
            Environment.GetEnvironmentVariable(DockerFactAttribute.EnabledVariable),
            "true",
            StringComparison.OrdinalIgnoreCase);
}
