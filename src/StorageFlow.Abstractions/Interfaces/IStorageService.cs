using StorageFlow.Abstractions.Models;

namespace StorageFlow.Abstractions.Interfaces;

/// <summary>
/// Primary fluent entry point for all storage operations.
/// </summary>
public interface IStorageService : IStorageOperationBuilder
{
}

/// <summary>
/// Builds an upload operation or creates an object operation context.
/// </summary>
public interface IStorageOperationBuilder
{
    /// <summary>Selects the provider used by the operation.</summary>
    IStorageOperationBuilder Provider(StorageProviderToken provider);

    /// <summary>Applies a typed storage profile to the operation.</summary>
    IStorageOperationBuilder Profile<TProfileKey>()
        where TProfileKey : IStorageProfileKey;

    /// <summary>Selects the validation policy used by the upload.</summary>
    IStorageOperationBuilder Validation<TPolicyKey>()
        where TPolicyKey : IValidationPolicyKey;

    /// <summary>Selects the naming policy used by the upload.</summary>
    IStorageOperationBuilder Naming<TPolicyKey>()
        where TPolicyKey : INamingPolicyKey;

    /// <summary>Selects the presigned URL policy used for post-upload cache warm-up.</summary>
    IStorageOperationBuilder PresignedUrl<TPolicyKey>()
        where TPolicyKey : IPresignedUrlPolicyKey;

    /// <summary>Adds or replaces one metadata value for the uploaded object.</summary>
    IStorageOperationBuilder Metadata(string key, string value);

    /// <summary>Adds or replaces metadata values for the uploaded object.</summary>
    IStorageOperationBuilder Metadata(IReadOnlyDictionary<string, string> values);

    /// <summary>Sets the Cache-Control header stored with the object.</summary>
    IStorageOperationBuilder CacheControl(string value);

    /// <summary>Sets the Content-Disposition header stored with the object.</summary>
    IStorageOperationBuilder ContentDisposition(string value);

    /// <summary>Sets the stream and file information used by the upload.</summary>
    IStorageOperationBuilder FromStream(
        Stream content,
        string fileName,
        string? contentType = null,
        long? contentLength = null);

    /// <summary>Uploads using the bucket supplied by the selected profile.</summary>
    Task<StorageResult<UploadResult>> UploadAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Uploads using the specified bucket.</summary>
    Task<StorageResult<UploadResult>> UploadAsync(
        string bucket,
        CancellationToken cancellationToken = default);

    /// <summary>Creates an object context using the bucket supplied by the selected profile.</summary>
    IStorageObjectBuilder Object(string objectKey);

    /// <summary>Creates an object context using the specified bucket and object key.</summary>
    IStorageObjectBuilder Object(string bucket, string objectKey);

    /// <summary>Creates an object collection context using the bucket supplied by the selected profile.</summary>
    IStorageObjectCollectionBuilder Objects(IReadOnlyList<string> objectKeys);

    /// <summary>Creates an object collection context using the specified bucket.</summary>
    IStorageObjectCollectionBuilder Objects(
        string bucket,
        IReadOnlyList<string> objectKeys);
}

/// <summary>
/// Performs operations against one object.
/// </summary>
public interface IStorageObjectBuilder
{
    /// <summary>Downloads the selected object.</summary>
    Task<StorageResult<DownloadResult>> DownloadAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Deletes the selected object.</summary>
    Task<StorageResult> DeleteAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Checks whether the selected object exists.</summary>
    Task<StorageResult<bool>> ExistsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Generates a presigned URL using the policy supplied by the selected profile.</summary>
    Task<StorageResult<string>> GetPresignedUrlAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Generates a presigned URL using a typed policy.</summary>
    Task<StorageResult<string>> GetPresignedUrlAsync<TPolicyKey>(
        CancellationToken cancellationToken = default)
        where TPolicyKey : IPresignedUrlPolicyKey;

    /// <summary>Generates a stable public CDN delivery URL using a typed policy.</summary>
    StorageResult<ObjectDeliveryUrlResult> GetDeliveryUrl<TPolicyKey>()
        where TPolicyKey : IDeliveryUrlPolicyKey;
}

/// <summary>
/// Generates stable public CDN delivery URLs for an ordered object-key collection.
/// </summary>
public interface IStorageObjectCollectionBuilder
{
    /// <summary>
    /// Generates delivery URLs while preserving input order and duplicate keys.
    /// </summary>
    StorageResult<IReadOnlyList<ObjectDeliveryUrlResult>> GetDeliveryUrls<TPolicyKey>()
        where TPolicyKey : IDeliveryUrlPolicyKey;
}
