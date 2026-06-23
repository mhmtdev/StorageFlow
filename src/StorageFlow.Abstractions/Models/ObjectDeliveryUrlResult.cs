namespace StorageFlow.Abstractions.Models;

/// <summary>
/// Represents delivery URL resolution for one object key.
/// </summary>
public class ObjectDeliveryUrlResult
{
    /// <summary>The input object key.</summary>
    public required string ObjectKey { get; init; }

    /// <summary>The generated CDN URL when resolution succeeds.</summary>
    public string? Url { get; init; }

    /// <summary>Whether this object key was resolved successfully.</summary>
    public bool IsSuccess => Error is null;

    /// <summary>The item-level validation error, if any.</summary>
    public StorageError? Error { get; init; }
}
