namespace StorageFlow.Abstractions.Models;

/// <summary>
/// Describes an error that occurred during a storage operation.
/// </summary>
public sealed class StorageError
{
    /// <summary>Categorised error code.</summary>
    public StorageErrorCode Code { get; init; }

    /// <summary>Human-readable error message.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// The underlying exception, if available.
    /// For provider errors this is the original SDK exception wrapped inside <c>StorageProviderException</c>.
    /// </summary>
    public Exception? InnerException { get; init; }

    /// <summary>Creates a <see cref="StorageError"/> from an error code and message.</summary>
    public static StorageError Create(StorageErrorCode code, string message, Exception? inner = null) =>
        new() { Code = code, Message = message, InnerException = inner };
}

/// <summary>
/// Categorises the type of error returned in a <see cref="StorageError"/>.
/// </summary>
public enum StorageErrorCode
{
    /// <summary>File failed one or more validation checks (size, extension, MIME type, or file signature).</summary>
    ValidationFailed,

    /// <summary>The storage provider reported an error (e.g. network failure, permission denied by the SDK).</summary>
    ProviderError,

    /// <summary>The requested object key was not found in the bucket.</summary>
    ObjectNotFound,

    /// <summary>The specified bucket does not exist or is not accessible.</summary>
    BucketNotFound,

    /// <summary>The operation was rejected due to insufficient permissions.</summary>
    PermissionDenied,

    /// <summary>An unknown or unclassified error occurred.</summary>
    Unknown
}

