using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Sample.Api.Configuration;

namespace StorageFlow.Sample.Api.Endpoints;

internal static class DownloadEndpoints
{
    internal static RouteGroupBuilder MapDownloadEndpoints(
        this RouteGroupBuilder group)
    {
        group.MapGet("/download/{**objectKey}", DownloadAsync)
            .WithName("DownloadObject")
            .WithTags("StorageFlow Download");

        return group;
    }

    private static async Task<IResult> DownloadAsync(
        string objectKey,
        IStorageService storage,
        StorageBackendOptions options,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await storage
            .Object(options.Bucket, objectKey)
            .DownloadAsync(cancellationToken);

        if (!result.IsSuccess)
            return StorageHttpResults.FromError(result.Error!);

        var download = result.Value!;
        if (download.ContentLength is not null)
            httpContext.Response.ContentLength = download.ContentLength;

        return Results.File(
            download.Content,
            download.ContentType ?? "application/octet-stream",
            Path.GetFileName(objectKey));
    }
}
