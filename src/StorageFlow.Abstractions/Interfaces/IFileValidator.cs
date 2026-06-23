namespace StorageFlow.Abstractions.Interfaces;

/// <summary>
/// Context passed to each <see cref="IFileValidator"/> during the validation pipeline.
/// </summary>
public sealed class FileValidationContext
{
    /// <summary>The file stream to validate. Position may be reset by validators.</summary>
    public required Stream Content { get; init; }

    /// <summary>The original file name provided by the caller (including extension).</summary>
    public required string FileName { get; init; }

    /// <summary>The declared MIME type provided by the caller (e.g. from an HTTP Content-Type header).</summary>
    public string? ContentType { get; init; }

    /// <summary>Total byte length of the content stream, if known.</summary>
    public long? ContentLength { get; init; }
}

/// <summary>
/// Result returned by a single <see cref="IFileValidator"/>.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>Whether this validation step passed.</summary>
    public bool IsValid { get; init; }

    /// <summary>Human-readable failure message. Null when <see cref="IsValid"/> is <c>true</c>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Returns a passing result.</summary>
    public static ValidationResult Success() => new() { IsValid = true };

    /// <summary>Returns a failing result with the given message.</summary>
    public static ValidationResult Failure(string errorMessage) =>
        new() { IsValid = false, ErrorMessage = errorMessage };
}

/// <summary>
/// Represents a single file validation step in the upload pipeline.
/// Built-in validators run in fixed order (size → extension → MIME → signature).
/// Custom validators are inserted by their <see cref="Order"/> value.
/// </summary>
public interface IFileValidator
{
    /// <summary>
    /// Determines the execution order relative to other validators.
    /// Built-in validators use 10, 20, 30, 40. Use values outside this range for custom validators.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Validates the file described by <paramref name="context"/>.
    /// </summary>
    Task<ValidationResult> ValidateAsync(
        FileValidationContext context,
        CancellationToken cancellationToken = default);
}

