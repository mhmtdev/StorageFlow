namespace StorageFlow.Provider.RustFs;

/// <summary>
/// Configuration options for the RustFS storage provider.
/// RustFS is S3-compatible; this provider uses the AWS SDK for .NET to connect,
/// which matches the official RustFS documentation examples.
/// </summary>
public sealed class RustFsProviderOptions
{
    /// <summary>
    /// RustFS server URL including scheme (e.g. "http://localhost:9000" or "https://rustfs.example.com").
    /// This is passed as the S3 service URL override.
    /// </summary>
    public required string ServiceUrl { get; set; }

    /// <summary>Access key.</summary>
    public required string AccessKey { get; set; }

    /// <summary>Secret key.</summary>
    public required string SecretKey { get; set; }

    /// <summary>
    /// AWS region name. RustFS accepts any value here; "us-east-1" is a safe default.
    /// </summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// When <c>true</c>, forces path-style URLs (required for most self-hosted S3-compatible servers).
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool ForcePathStyle { get; set; } = true;
}
