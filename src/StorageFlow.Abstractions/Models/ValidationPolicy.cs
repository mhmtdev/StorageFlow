namespace StorageFlow.Abstractions.Models;

/// <summary>
/// Defines file validation rules used by the upload pipeline.
/// </summary>
public sealed class ValidationPolicy
{
    /// <summary>Maximum allowed file size in bytes. <c>null</c> means no limit.</summary>
    public long? MaxFileSizeBytes { get; set; }

    /// <summary>Minimum required file size in bytes. <c>null</c> means no minimum.</summary>
    public long? MinFileSizeBytes { get; set; }

    /// <summary>Allowed file extensions. Empty or <c>null</c> allows all extensions.</summary>
    public IReadOnlyList<string>? AllowedExtensions { get; set; }

    /// <summary>Blocked file extensions.</summary>
    public IReadOnlyList<string>? BlockedExtensions { get; set; }

    /// <summary>Allowed MIME types. Empty or <c>null</c> allows all MIME types.</summary>
    public IReadOnlyList<string>? AllowedMimeTypes { get; set; }

    /// <summary>Whether the file signature must match the declared extension.</summary>
    public bool RequireValidSignature { get; set; }
}
