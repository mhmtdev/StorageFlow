using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Abstractions.Models;

namespace StorageFlow.Core.Validation;

/// <summary>
/// Validates the file extension against allowed and blocked lists defined in the policy.
/// Runs second in the pipeline (Order = 20).
/// </summary>
public sealed class ExtensionValidator : IFileValidator
{
    private readonly ValidationPolicy _policy;

    /// <inheritdoc />
    public int Order => 20;

    /// <param name="policy">The validation policy to enforce.</param>
    public ExtensionValidator(ValidationPolicy policy)
    {
        _policy = policy;
    }

    /// <inheritdoc />
    public Task<ValidationResult> ValidateAsync(
        FileValidationContext context,
        CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(context.FileName);

        if (_policy.BlockedExtensions is { Count: > 0 })
        {
            if (_policy.BlockedExtensions.Any(b => b.Equals(ext, StringComparison.OrdinalIgnoreCase)))
                return Task.FromResult(ValidationResult.Failure(
                    $"File extension '{ext}' is not allowed."));
        }

        if (_policy.AllowedExtensions is { Count: > 0 })
        {
            if (!_policy.AllowedExtensions.Any(a => a.Equals(ext, StringComparison.OrdinalIgnoreCase)))
                return Task.FromResult(ValidationResult.Failure(
                    $"File extension '{ext}' is not in the allowed list: {string.Join(", ", _policy.AllowedExtensions)}."));
        }

        return Task.FromResult(ValidationResult.Success());
    }
}
