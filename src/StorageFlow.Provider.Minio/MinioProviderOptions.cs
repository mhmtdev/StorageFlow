namespace StorageFlow.Provider.Minio;

/// <summary>
/// Configuration options for the MinIO storage provider.
/// </summary>
public sealed class MinioProviderOptions
{
    /// <summary>MinIO server endpoint (e.g. "localhost:9000" or "play.min.io").</summary>
    public required string Endpoint { get; set; }

    /// <summary>Access key (username).</summary>
    public required string AccessKey { get; set; }

    /// <summary>Secret key (password).</summary>
    public required string SecretKey { get; set; }

    /// <summary>When <c>true</c>, uses HTTPS. Default is <c>false</c> for local development.</summary>
    public bool UseSSL { get; set; } = false;

    /// <summary>Optional region. Defaults to "us-east-1" if not set.</summary>
    public string Region { get; set; } = "us-east-1";
}

