namespace StorageFlow.Abstractions.Interfaces;

/// <summary>
/// Context passed to <see cref="IFileNamingStrategy.GenerateAsync"/> to produce the final object key.
/// </summary>
public sealed class FileNamingContext
{
    /// <summary>The original file name provided by the caller (including extension).</summary>
    public required string OriginalFileName { get; init; }

    /// <summary>The file extension including the leading dot (e.g. ".jpg").</summary>
    public string Extension => Path.GetExtension(OriginalFileName);

    /// <summary>The UTC date/time at which the upload was initiated.</summary>
    public DateTimeOffset UploadedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Pattern supplied by the selected naming policy.
    /// Custom strategies may also consume this value.
    /// </summary>
    public string? Pattern { get; init; }
}

/// <summary>
/// Generates the final object key (storage path) for an uploaded file.
/// Runs as step 2 of the upload pipeline, after validation and before provider upload.
/// </summary>
public interface IFileNamingStrategy
{
    /// <summary>
    /// Generates a unique object key for the file described by <paramref name="context"/>.
    /// </summary>
    /// <param name="context">Contextual information about the file being uploaded.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved object key (e.g. "2026/06/my-photo-a1b2c3.jpg").</returns>
    Task<string> GenerateAsync(
        FileNamingContext context,
        CancellationToken cancellationToken = default);
}
