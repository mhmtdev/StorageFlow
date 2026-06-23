using StorageFlow.Abstractions.Models;

namespace StorageFlow.Abstractions.Interfaces;

/// <summary>
/// Represents a storage provider that performs the actual object storage operations.
/// All provider implementations must wrap SDK-specific exceptions as
/// <see cref="Exceptions.StorageProviderException"/> — provider-specific exceptions
/// must never leak to application code.
/// </summary>
public interface IStorageProvider
{
    /// <summary>
    /// Gets the unique name of this provider instance (e.g. "minio", "s3").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Uploads an object to the specified bucket with the given key.
    /// </summary>
    /// <param name="bucket">The destination bucket.</param>
    /// <param name="objectKey">The generated object key.</param>
    /// <param name="content">The readable content stream. The provider must not dispose it.</param>
    /// <param name="contentType">The optional content type.</param>
    /// <param name="contentLength">The optional known content length, including for non-seekable streams.</param>
    /// <param name="metadata">Optional object metadata.</param>
    /// <param name="headers">Optional standard representation headers.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<UploadResult> UploadAsync(
        string bucket,
        string objectKey,
        Stream content,
        string? contentType = null,
        long? contentLength = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        UploadHeaders? headers = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads an object from the specified bucket and returns its stream.
    /// The caller is responsible for disposing the returned stream.
    /// </summary>
    Task<DownloadResult> DownloadAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an object from the specified bucket.
    /// </summary>
    Task DeleteAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <c>true</c> if an object with the given key exists in the bucket.
    /// </summary>
    Task<bool> ExistsAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a presigned URL for the specified object.
    /// </summary>
    /// <param name="bucket">The bucket containing the object.</param>
    /// <param name="objectKey">The object key.</param>
    /// <param name="expiration">How long the presigned URL remains valid.</param>
    /// <param name="httpMethod">The HTTP method allowed by the presigned URL (GET, PUT, etc.).</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    Task<string> GetPresignedUrlAsync(
        string bucket,
        string objectKey,
        TimeSpan expiration,
        HttpMethod httpMethod,
        CancellationToken cancellationToken = default);
}
