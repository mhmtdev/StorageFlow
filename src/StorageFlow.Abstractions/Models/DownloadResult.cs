using System.Collections.ObjectModel;

namespace StorageFlow.Abstractions.Models;

/// <summary>
/// Contains a streaming object download and its provider metadata.
/// </summary>
public class DownloadResult
{
    /// <summary>
    /// The readable object content. The caller owns and must dispose this stream.
    /// </summary>
    public required Stream Content { get; init; }

    /// <summary>The stored content type.</summary>
    public string? ContentType { get; init; }

    /// <summary>The object length in bytes.</summary>
    public long? ContentLength { get; init; }

    /// <summary>The normalized entity tag without surrounding quotes.</summary>
    public string? ETag { get; init; }

    /// <summary>The provider-reported last modification timestamp.</summary>
    public DateTimeOffset? LastModified { get; init; }

    /// <summary>User-defined object metadata.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
}
