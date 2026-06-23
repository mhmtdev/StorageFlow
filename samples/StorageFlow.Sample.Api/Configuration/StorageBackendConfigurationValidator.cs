using StorageFlow.Abstractions.Exceptions;

namespace StorageFlow.Sample.Api.Configuration;

public sealed class StorageBackendConfigurationValidator
{
    public void Validate(StorageBackendOptions options)
    {
        if (!options.Minio.Enabled && !options.S3.Enabled)
            throw new StorageConfigurationException(
                "At least one provider must be enabled in StorageFlow.");

        if (options.Minio.Enabled)
            ValidateRequiredPair(
                "Minio",
                options.Minio.AccessKey,
                options.Minio.SecretKey);

        if (options.S3.Enabled)
            ValidateOptionalS3Credentials(options.S3);
    }

    private static void ValidateRequiredPair(
        string provider,
        string accessKey,
        string secretKey)
    {
        if (string.IsNullOrWhiteSpace(accessKey))
            ThrowMissing($"StorageFlow:{provider}:AccessKey");

        if (string.IsNullOrWhiteSpace(secretKey))
            ThrowMissing($"StorageFlow:{provider}:SecretKey");
    }

    private static void ValidateOptionalS3Credentials(S3Options options)
    {
        var hasAccessKey = !string.IsNullOrWhiteSpace(options.AccessKey);
        var hasSecretKey = !string.IsNullOrWhiteSpace(options.SecretKey);
        if (hasAccessKey != hasSecretKey)
        {
            ThrowMissing(hasAccessKey
                ? "StorageFlow:S3:SecretKey"
                : "StorageFlow:S3:AccessKey");
        }

        if (!string.IsNullOrWhiteSpace(options.SessionToken) && !hasAccessKey)
        {
            throw new StorageConfigurationException(
                "Configuration 'StorageFlow:S3:SessionToken' requires " +
                "StorageFlow:S3:AccessKey and StorageFlow:S3:SecretKey.");
        }
    }

    private static void ThrowMissing(string key) =>
        throw new StorageConfigurationException(
            $"Required configuration '{key}' is missing.");
}
