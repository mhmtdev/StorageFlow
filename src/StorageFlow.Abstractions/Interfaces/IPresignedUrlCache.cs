namespace StorageFlow.Abstractions.Interfaces;

/// <summary>
/// Optional cache for presigned URLs. Implementations are provided by extension packages
/// (e.g. Redis) and consumed by <see cref="IStorageService"/> when registered in DI.
/// </summary>
public interface IPresignedUrlCache
{
    /// <summary>
    /// Attempts to retrieve a cached presigned URL.
    /// Returns <c>null</c> when the entry does not exist or has expired.
    /// </summary>
    Task<string?> GetAsync(
        string providerName,
        string bucket,
        string objectKey,
        string policyKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a presigned URL with an expiration matching the policy TTL.
    /// </summary>
    Task SetAsync(
        string providerName,
        string bucket,
        string objectKey,
        string policyKey,
        string url,
        TimeSpan policyExpiration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a cached presigned URL entry.
    /// </summary>
    Task RemoveAsync(
        string providerName,
        string bucket,
        string objectKey,
        string policyKey,
        CancellationToken cancellationToken = default);
}
