namespace StorageFlow.Abstractions.Models;

/// <summary>
/// Represents the outcome of a storage operation.
/// All public API methods return this type instead of throwing exceptions directly.
/// </summary>
public class StorageResult
{
    /// <summary>Whether the operation completed successfully.</summary>
    public bool IsSuccess { get; init; }

    /// <summary>Error details when <see cref="IsSuccess"/> is <c>false</c>; otherwise <c>null</c>.</summary>
    public StorageError? Error { get; init; }

    /// <summary>Creates a successful result.</summary>
    public static StorageResult Success() => new() { IsSuccess = true };

    /// <summary>Creates a failed result with the given error.</summary>
    public static StorageResult Failure(StorageError error) =>
        new() { IsSuccess = false, Error = error };
}

/// <summary>
/// Represents the outcome of a storage operation that returns a value.
/// </summary>
/// <typeparam name="T">The type of the result value.</typeparam>
public class StorageResult<T> : StorageResult
{
    /// <summary>The result value when <see cref="StorageResult.IsSuccess"/> is <c>true</c>; otherwise <c>default</c>.</summary>
    public T? Value { get; init; }

    /// <summary>Creates a successful result carrying the given value.</summary>
    public static StorageResult<T> Success(T value) =>
        new() { IsSuccess = true, Value = value };

    /// <summary>Creates a failed result with the given error.</summary>
    public static new StorageResult<T> Failure(StorageError error) =>
        new() { IsSuccess = false, Error = error };
}

