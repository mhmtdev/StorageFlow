using StorageFlow.Tests.Integration.Contracts;
using StorageFlow.Tests.Integration.Fixtures;

namespace StorageFlow.Tests.Integration.Providers;

[Collection(CollectionName)]
public sealed class S3LocalStackProviderContractTests(LocalStackFixture fixture)
    : ObjectStorageProviderContract(fixture)
{
    public const string CollectionName = "S3 LocalStack";
}

[CollectionDefinition(S3LocalStackProviderContractTests.CollectionName)]
public sealed class S3LocalStackCollection : ICollectionFixture<LocalStackFixture>;
