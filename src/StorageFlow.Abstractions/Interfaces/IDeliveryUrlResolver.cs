using StorageFlow.Abstractions.Models;

namespace StorageFlow.Abstractions.Interfaces;

/// <summary>
/// Resolves stable public CDN URLs without storage or network access.
/// </summary>
public interface IDeliveryUrlResolver
{
    /// <summary>Resolves one object key using a normalized delivery policy.</summary>
    StorageResult<ObjectDeliveryUrlResult> Resolve(
        DeliveryUrlPolicy policy,
        string bucket,
        string objectKey);

    /// <summary>Resolves an ordered collection without network or storage access.</summary>
    StorageResult<IReadOnlyList<ObjectDeliveryUrlResult>> ResolveMany(
        DeliveryUrlPolicy policy,
        string bucket,
        IReadOnlyList<string> objectKeys);
}
