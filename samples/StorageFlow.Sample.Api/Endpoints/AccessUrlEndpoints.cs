using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Sample.Api.Configuration;
using StorageFlow.Sample.Api.Policies;

namespace StorageFlow.Sample.Api.Endpoints;

internal static class AccessUrlEndpoints
{
    internal static RouteGroupBuilder MapAccessUrlEndpoints(
        this RouteGroupBuilder group)
    {
        group.MapGet("/presigned/{**objectKey}", PresignedUrlAsync)
            .WithName("GetPresignedUrl")
            .WithTags("StorageFlow URLs");
        group.MapGet("/delivery/{**objectKey}", DeliveryUrl)
            .WithName("GetDeliveryUrl")
            .WithTags("StorageFlow URLs");

        return group;
    }

    private static async Task<IResult> PresignedUrlAsync(
        string objectKey,
        IStorageService storage,
        StorageBackendOptions options,
        CancellationToken cancellationToken)
    {
        var result = await storage
            .Object(options.Bucket, objectKey)
            .GetPresignedUrlAsync<DownloadPolicy>(cancellationToken);

        return result.IsSuccess
            ? Results.Ok(new { objectKey, url = result.Value })
            : StorageHttpResults.FromError(result.Error!);
    }

    private static IResult DeliveryUrl(
        string objectKey,
        IStorageService storage,
        StorageBackendOptions options)
    {
        var result = storage
            .Object(options.Bucket, objectKey)
            .GetDeliveryUrl<PublicAssetDelivery>();

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : StorageHttpResults.FromError(result.Error!);
    }
}
