using StorageFlow.Abstractions.Models;

namespace StorageFlow.Sample.Api.Endpoints;

internal static class StorageHttpResults
{
    internal static IResult FromUpload(StorageResult<UploadResult> result) =>
        result.IsSuccess
            ? Results.Ok(result.Value)
            : FromError(result.Error!);

    internal static IResult FromError(StorageError error) =>
        Results.Problem(
            statusCode: ToStatusCode(error.Code),
            title: error.Code.ToString(),
            detail: error.Message);

    private static int ToStatusCode(StorageErrorCode code) => code switch
    {
        StorageErrorCode.ValidationFailed => StatusCodes.Status400BadRequest,
        StorageErrorCode.ObjectNotFound => StatusCodes.Status404NotFound,
        StorageErrorCode.BucketNotFound => StatusCodes.Status404NotFound,
        StorageErrorCode.PermissionDenied => StatusCodes.Status403Forbidden,
        StorageErrorCode.ProviderError => StatusCodes.Status502BadGateway,
        _ => StatusCodes.Status500InternalServerError
    };
}
