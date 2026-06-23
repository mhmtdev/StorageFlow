using StorageFlow.Abstractions.Models;
using StorageFlow.Testing;
using StorageFlow.Tests.Component.Infrastructure;

namespace StorageFlow.Tests.Component.Providers;

public class ProviderRoutingFlowTests
{
    [Fact]
    public async Task ExplicitTestToken_RoutesUpload()
    {
        await using var services = ComponentTestHost.Build();

        var result = await services.Storage()
            .Provider(SFTestProvider.InMemory)
            .FromStream(new MemoryStream([1, 2, 3]), "file.bin")
            .UploadAsync("bucket");

        Assert.True(result.IsSuccess);
        Assert.Equal("memory", result.Value!.ProviderName);
    }

    [Fact]
    public async Task UnregisteredOfficialToken_FailsAtTerminalOperation()
    {
        await using var services = ComponentTestHost.Build();

        var result = await services.Storage()
            .Provider(SFProvider.Minio)
            .FromStream(new MemoryStream([1, 2, 3]), "file.bin")
            .UploadAsync("bucket");

        Assert.False(result.IsSuccess);
        Assert.Contains("not registered", result.Error!.Message);
    }
}
