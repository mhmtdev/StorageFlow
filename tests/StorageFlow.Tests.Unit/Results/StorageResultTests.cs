using StorageFlow.Abstractions.Models;

namespace StorageFlow.Tests.Unit.Results;

public class StorageResultTests
{
    [Fact]
    public void Success_SetsIsSuccessTrue()
    {
        var result = StorageResult.Success();

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_SetsIsSuccessFalse()
    {
        var error = StorageError.Create(StorageErrorCode.ValidationFailed, "bad file");
        var result = StorageResult.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.Equal(StorageErrorCode.ValidationFailed, result.Error!.Code);
    }
}
