using System.Collections.Concurrent;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.DependencyInjection;
using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Core.DependencyInjection;
using StorageFlow.Provider.S3;

namespace StorageFlow.Tests.Cloud.Infrastructure;

public sealed class AwsCloudFixture : IAsyncLifetime
{
    private readonly ConcurrentDictionary<string, byte> _createdKeys = new();
    private ServiceProvider? _services;
    private IAmazonS3? _cleanupClient;

    public string Region { get; private set; } = string.Empty;
    public string Bucket { get; private set; } = string.Empty;
    public string Prefix { get; private set; } = string.Empty;
    public IStorageService Storage => _services!.GetRequiredService<IStorageService>();
    public IAmazonS3 ManagementClient => _cleanupClient!;

    public Task InitializeAsync()
    {
        Region = Require("STORAGEFLOW_TEST_AWS_REGION");
        Bucket = Require("STORAGEFLOW_TEST_AWS_BUCKET");
        var prefixRoot = Environment.GetEnvironmentVariable("STORAGEFLOW_TEST_AWS_PREFIX");
        if (string.IsNullOrWhiteSpace(prefixRoot))
            prefixRoot = "storageflow-tests";

        Prefix = $"{prefixRoot.Trim('/')}/{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        var services = new ServiceCollection();
        services.AddStorageFlow(options =>
        {
            options.Naming
                .AddPolicy<CloudNaming>(policy =>
                    policy.UsePattern($"{Prefix}/{{guid}}{{ext}}"))
                .AsDefault();
            options.PresignedUrls.AddPolicy<CloudDownloadPolicy>(policy =>
            {
                policy.HttpMethod = HttpMethod.Get;
                policy.Expiration = TimeSpan.FromMinutes(5);
            });
            options.Providers.UseS3(s3 => s3.Configure(config =>
            {
                config.Region = Region;
                // Intentionally omit static credentials to exercise the AWS SDK default chain.
            })).AsDefault();
        });

        _services = services.BuildServiceProvider();
        _cleanupClient = new AmazonS3Client(RegionEndpoint.GetBySystemName(Region));
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_cleanupClient is not null)
        {
            var failures = new List<Exception>();
            foreach (var key in _createdKeys.Keys)
            {
                try
                {
                    await _cleanupClient.DeleteObjectAsync(new DeleteObjectRequest
                    {
                        BucketName = Bucket,
                        Key = key
                    });
                }
                catch (Exception ex)
                {
                    failures.Add(new InvalidOperationException(
                        $"Failed to clean AWS cloud test object '{key}'.",
                        ex));
                }
            }

            _cleanupClient.Dispose();
            if (failures.Count > 0)
                throw new AggregateException("AWS cloud test cleanup failed.", failures);
        }

        if (_services is not null)
            await _services.DisposeAsync();
    }

    public void Track(string objectKey) => _createdKeys.TryAdd(objectKey, 0);

    public void Untrack(string objectKey) => _createdKeys.TryRemove(objectKey, out _);

    private static string Require(string key) =>
        Environment.GetEnvironmentVariable(key) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException(
                $"Required cloud test configuration '{key}' is missing.");

    public sealed class CloudNaming : INamingPolicyKey;
    public sealed class CloudDownloadPolicy : IPresignedUrlPolicyKey;
}
