using StorageFlow.Tests.Integration.Contracts;
using StorageFlow.Tests.Integration.Fixtures;
using StorageFlow.Tests.Integration.Infrastructure;

namespace StorageFlow.Tests.Integration.Providers;

[Collection(CollectionName)]
public sealed class RustFsProviderContractTests(RustFsFixture fixture)
    : ObjectStorageProviderContract(fixture)
{
    public const string CollectionName = "RustFS";

    [DockerFact]
    public Task InvalidCredentials_ReturnPermissionDenied() =>
        AssertInvalidCredentialsAreRejectedAsync();
}

[CollectionDefinition(RustFsProviderContractTests.CollectionName)]
public sealed class RustFsCollection : ICollectionFixture<RustFsFixture>;
