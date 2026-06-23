using Microsoft.Extensions.Configuration;
using StorageFlow.Abstractions.Exceptions;
using StorageFlow.Sample.Api.Configuration;

namespace StorageFlow.Tests.Component.Sample;

public class SampleConfigurationTests
{
    private readonly StorageBackendConfigurationValidator _validator = new();

    [Fact]
    public void Validate_EnabledMinioWithoutAccessKeyFailsAtStartup()
    {
        var options = new StorageBackendOptions
        {
            Minio =
            {
                Enabled = true,
                SecretKey = "must-not-appear"
            }
        };

        var exception = Assert.Throws<StorageConfigurationException>(
            () => _validator.Validate(options));

        Assert.Contains("StorageFlow:Minio:AccessKey", exception.Message);
        Assert.DoesNotContain(options.Minio.SecretKey, exception.Message);
    }

    [Fact]
    public void Validate_DisabledMinioUsesEnabledS3WithoutStaticCredentials()
    {
        var options = new StorageBackendOptions
        {
            Minio = { Enabled = false },
            S3 = { Enabled = true }
        };

        _validator.Validate(options);
    }

    [Fact]
    public void Validate_EnabledS3WithoutStaticCredentialsUsesDefaultChain()
    {
        var options = new StorageBackendOptions
        {
            Minio = { Enabled = false },
            S3 = { Enabled = true }
        };

        _validator.Validate(options);
    }

    [Fact]
    public void Validate_EnabledS3WithPartialStaticCredentialsFails()
    {
        var options = new StorageBackendOptions
        {
            Minio = { Enabled = false },
            S3 =
            {
                Enabled = true,
                AccessKey = "access"
            }
        };

        var exception = Assert.Throws<StorageConfigurationException>(
            () => _validator.Validate(options));

        Assert.Contains("StorageFlow:S3:SecretKey", exception.Message);
        Assert.DoesNotContain(options.S3.AccessKey, exception.Message);
    }

    [Fact]
    public void Validate_S3SessionTokenWithoutCredentialPairFails()
    {
        var options = new StorageBackendOptions
        {
            Minio = { Enabled = false },
            S3 =
            {
                Enabled = true,
                SessionToken = "must-not-appear"
            }
        };

        var exception = Assert.Throws<StorageConfigurationException>(
            () => _validator.Validate(options));

        Assert.Contains("StorageFlow:S3:SessionToken", exception.Message);
        Assert.DoesNotContain(options.S3.SessionToken, exception.Message);
    }

    [Fact]
    public void Validate_WhenNoProviderEnabledFailsAtStartup()
    {
        var options = new StorageBackendOptions
        {
            Minio = { Enabled = false },
            S3 = { Enabled = false }
        };

        var exception = Assert.Throws<StorageConfigurationException>(
            () => _validator.Validate(options));

        Assert.Contains("At least one provider", exception.Message);
    }

    [Fact]
    public void EnvironmentStyleKeysOverrideBaseConfiguration()
    {
        var baseValues = new Dictionary<string, string?>
        {
            ["StorageFlow:Minio:Enabled"] = "true",
            ["StorageFlow:Minio:AccessKey"] = string.Empty,
            ["StorageFlow:Minio:SecretKey"] = string.Empty
        };
        var environmentValues = new Dictionary<string, string?>
        {
            ["StorageFlow:Minio:AccessKey"] = "environment-access",
            ["StorageFlow:Minio:SecretKey"] = "environment-secret"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(baseValues)
            .AddInMemoryCollection(environmentValues)
            .Build();
        var options = configuration
            .GetSection(StorageBackendOptions.SectionName)
            .Get<StorageBackendOptions>()!;

        _validator.Validate(options);
        Assert.Equal("environment-access", options.Minio.AccessKey);
        Assert.Equal("environment-secret", options.Minio.SecretKey);
    }
}
