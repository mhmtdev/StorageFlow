namespace StorageFlow.Sample.Api.Endpoints;

internal static class StorageEndpointRouteBuilderExtensions
{
    internal static IEndpointRouteBuilder MapStorageEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/storage");

        group.MapUploadEndpoints();
        group.MapDownloadEndpoints();
        group.MapObjectEndpoints();
        group.MapAccessUrlEndpoints();

        return endpoints;
    }
}
