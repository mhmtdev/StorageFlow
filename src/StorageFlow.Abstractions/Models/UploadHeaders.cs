namespace StorageFlow.Abstractions.Models;

/// <summary>
/// Standard HTTP representation headers stored with an object.
/// </summary>
public class UploadHeaders
{
    /// <summary>The Cache-Control response header value.</summary>
    public string? CacheControl { get; init; }

    /// <summary>The Content-Disposition response header value.</summary>
    public string? ContentDisposition { get; init; }
}
