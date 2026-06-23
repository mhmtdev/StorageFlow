using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using StorageFlow.Abstractions.Interfaces;

namespace StorageFlow.Tests.Integration.Infrastructure;

public interface IProviderFixture
{
    string ProviderName { get; }
    string Bucket { get; }
    IStorageService Storage { get; }
    IAmazonS3 ManagementClient { get; }
    ServiceProvider CreateServicesWithCredentials(string accessKey, string secretKey);
}
