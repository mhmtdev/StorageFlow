using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Sample.Api.Configuration;

namespace StorageFlow.Sample.Api.Endpoints;

internal static class ObjectEndpoints
{
    internal static RouteGroupBuilder MapObjectEndpoints(
        this RouteGroupBuilder group)
    {
        group.MapDelete("/{**objectKey}", DeleteAsync)
            .WithName("DeleteObject")
            .WithTags("StorageFlow Objects");
        group.MapGet("/exists/{**objectKey}", ExistsAsync)
            .WithName("ObjectExists")
            .WithTags("StorageFlow Objects");

        return group;
    }

    private static async Task<IResult> DeleteAsync(
        string objectKey,
        IStorageService storage,
        StorageBackendOptions options,
        CancellationToken cancellationToken)
    {
        var result = await storage
            .Object(options.Bucket, objectKey)
            .DeleteAsync(cancellationToken);

        return result.IsSuccess
            ? Results.NoContent()
            : StorageHttpResults.FromError(result.Error!);
    }

    private static async Task<IResult> ExistsAsync(
        string objectKey,
        IStorageService storage,
        StorageBackendOptions options,
        CancellationToken cancellationToken)
    {
        var result = await storage
            .Object(options.Bucket, objectKey)
            .ExistsAsync(cancellationToken);

        return result.IsSuccess
            ? Results.Ok(new { objectKey, exists = result.Value })
            : StorageHttpResults.FromError(result.Error!);
    }
}
