using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Sample.Api.Configuration;
using StorageFlow.Sample.Api.Policies;

namespace StorageFlow.Sample.Api.Endpoints;

internal static class UploadEndpoints
{
    internal static RouteGroupBuilder MapUploadEndpoints(
        this RouteGroupBuilder group)
    {
        group.MapPost("/upload", UploadAsync)
            .DisableAntiforgery()
            .WithName("UploadObject")
            .WithTags("StorageFlow Upload");

        return group;
    }

    private static async Task<IResult> UploadAsync(
        IFormFile file,
        IStorageService storage,
        StorageBackendOptions options,
        CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();

        var result = await storage
            .Validation<DocumentsPolicy>()
            .CacheControl("private, max-age=0")
            .ContentDisposition("attachment")
            .Metadata("source", "minimal-sample")
            .FromStream(content, file.FileName, file.ContentType, file.Length)
            .UploadAsync(options.Bucket, cancellationToken);

        return StorageHttpResults.FromUpload(result);
    }
}
