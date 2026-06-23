using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Abstractions.Models;

namespace StorageFlow.Core.Validation;

/// <summary>
/// Validates the file size against <see cref="ValidationPolicy.MinFileSizeBytes"/>
/// and <see cref="ValidationPolicy.MaxFileSizeBytes"/>.
/// Runs first in the pipeline (Order = 10).
/// </summary>
public sealed class FileSizeValidator : IFileValidator
{
    private readonly ValidationPolicy _policy;

    /// <inheritdoc />
    public int Order => 10;

    /// <param name="policy">The validation policy to enforce.</param>
    public FileSizeValidator(ValidationPolicy policy)
    {
        _policy = policy;
    }

    /// <inheritdoc />
    public Task<ValidationResult> ValidateAsync(
        FileValidationContext context,
        CancellationToken cancellationToken = default)
    {
        var length = context.ContentLength ?? (context.Content.CanSeek ? context.Content.Length : (long?)null);

        if (length is null)
            return Task.FromResult(ValidationResult.Success()); // cannot determine size — skip

        if (_policy.MinFileSizeBytes.HasValue && length < _policy.MinFileSizeBytes.Value)
            return Task.FromResult(ValidationResult.Failure(
                $"File size {length} bytes is below the minimum of {_policy.MinFileSizeBytes.Value} bytes."));

        if (_policy.MaxFileSizeBytes.HasValue && length > _policy.MaxFileSizeBytes.Value)
            return Task.FromResult(ValidationResult.Failure(
                $"File size {length} bytes exceeds the maximum of {_policy.MaxFileSizeBytes.Value} bytes."));

        return Task.FromResult(ValidationResult.Success());
    }
}
