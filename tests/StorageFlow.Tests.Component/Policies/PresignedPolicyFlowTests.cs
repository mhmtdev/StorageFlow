using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Tests.Component.Infrastructure;

namespace StorageFlow.Tests.Component.Policies;

public class PresignedPolicyFlowTests
{
    private sealed class UnknownPolicy : IPresignedUrlPolicyKey;

    [Fact]
    public async Task UnknownPolicy_ReturnsConfigurationFailure()
    {
        await using var services = ComponentTestHost.Build();

        var result = await services.Storage()
            .Object("media", "photo.jpg")
            .GetPresignedUrlAsync<UnknownPolicy>();

        Assert.False(result.IsSuccess);
        Assert.Contains("Presigned URL policy", result.Error!.Message);
    }
}
