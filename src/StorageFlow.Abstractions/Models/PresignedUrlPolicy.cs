namespace StorageFlow.Abstractions.Models;

/// <summary>
/// Defines how a presigned URL is generated.
/// </summary>
public sealed class PresignedUrlPolicy
{
    /// <summary>How long the generated URL remains valid.</summary>
    public TimeSpan Expiration { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>The HTTP method permitted by the generated URL.</summary>
    public HttpMethod HttpMethod { get; set; } = HttpMethod.Get;
}
