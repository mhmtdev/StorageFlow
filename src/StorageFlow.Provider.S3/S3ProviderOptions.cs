namespace StorageFlow.Provider.S3;

/// <summary>
/// Configuration options for the AWS S3 storage provider.
/// </summary>
public sealed class S3ProviderOptions
{
    /// <summary>AWS region (e.g. "eu-central-1", "us-east-1").</summary>
    public required string Region { get; set; }

    /// <summary>AWS Access Key ID.</summary>
    public string? AccessKey { get; set; }

    /// <summary>AWS Secret Access Key.</summary>
    public string? SecretKey { get; set; }

    /// <summary>Optional session token used with temporary static credentials.</summary>
    public string? SessionToken { get; set; }

    /// <summary>
    /// Optional service URL override. Set this to use a custom S3-compatible endpoint
    /// (e.g. LocalStack: "http://localhost:4566").
    /// When <c>null</c>, the standard AWS endpoint for the region is used.
    /// </summary>
    public string? ServiceUrl { get; set; }
}
