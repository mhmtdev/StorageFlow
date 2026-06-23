namespace StorageFlow.Abstractions.Models;

/// <summary>
/// Contains the result of a successful upload operation.
/// </summary>
public sealed class UploadResult
{
    /// <summary>The final object key as stored in the provider (e.g. "2026/06/my-photo-a1b2c3.jpg").</summary>
    public required string ObjectKey { get; init; }

    /// <summary>The bucket the object was uploaded to.</summary>
    public required string Bucket { get; init; }

    /// <summary>The name of the provider that handled the upload.</summary>
    public required string ProviderName { get; init; }

    /// <summary>The size of the uploaded content in bytes.</summary>
    public long? SizeBytes { get; init; }

    /// <summary>The content type stored alongside the object.</summary>
    public string? ContentType { get; init; }

    /// <summary>The normalized provider entity tag without surrounding quotes.</summary>
    public string? ETag { get; init; }

    /// <summary>The UTC timestamp at which the upload completed.</summary>
    public DateTimeOffset UploadedAt { get; init; } = DateTimeOffset.UtcNow;
}
