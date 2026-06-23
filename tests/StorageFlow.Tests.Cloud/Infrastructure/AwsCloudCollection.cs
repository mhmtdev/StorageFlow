namespace StorageFlow.Tests.Cloud.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AwsCloudCollection : ICollectionFixture<AwsCloudFixture>
{
    public const string Name = "AWS cloud";
}
