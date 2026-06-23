using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Abstractions.Models;

namespace StorageFlow.Core.Validation;

/// <summary>
/// Validates the declared MIME type against the allowed list in the policy.
/// Runs third in the pipeline (Order = 30).
/// </summary>
public sealed class MimeTypeValidator : IFileValidator
{
    private readonly ValidationPolicy _policy;

    /// <inheritdoc />
    public int Order => 30;

    /// <param name="policy">The validation policy to enforce.</param>
    public MimeTypeValidator(ValidationPolicy policy)
    {
        _policy = policy;
    }

    /// <inheritdoc />
    public Task<ValidationResult> ValidateAsync(
        FileValidationContext context,
        CancellationToken cancellationToken = default)
    {
        if (_policy.AllowedMimeTypes is not { Count: > 0 })
            return Task.FromResult(ValidationResult.Success()); // no restriction

        if (string.IsNullOrWhiteSpace(context.ContentType))
            return Task.FromResult(ValidationResult.Failure(
                "Content-Type header is required but was not provided."));

        // Strip parameters (e.g. "image/jpeg; charset=utf-8" → "image/jpeg")
        var mimeType = context.ContentType.Split(';')[0].Trim();

        if (!_policy.AllowedMimeTypes.Any(m => m.Equals(mimeType, StringComparison.OrdinalIgnoreCase)))
            return Task.FromResult(ValidationResult.Failure(
                $"MIME type '{mimeType}' is not in the allowed list: {string.Join(", ", _policy.AllowedMimeTypes)}."));

        return Task.FromResult(ValidationResult.Success());
    }
}
