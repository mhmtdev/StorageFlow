namespace StorageFlow.Abstractions.Exceptions;

/// <summary>
/// Base exception for all StorageFlow framework errors.
/// These exceptions are used internally; the public API surfaces errors via <c>StorageResult</c>.
/// </summary>
public class StorageException : Exception
{
    /// <inheritdoc />
    public StorageException() { }

    /// <inheritdoc />
    public StorageException(string message) : base(message) { }

    /// <inheritdoc />
    public StorageException(string message, Exception? innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Thrown when a file fails one or more validation checks (size, extension, MIME type, or file signature).
/// </summary>
public sealed class StorageValidationException : StorageException
{
    /// <summary>The name of the validator that produced this failure.</summary>
    public string? ValidatorName { get; }

    /// <inheritdoc />
    public StorageValidationException(string message, string? validatorName = null)
        : base(message)
    {
        ValidatorName = validatorName;
    }
}

/// <summary>
/// Thrown when the underlying storage provider (S3, MinIO, etc.) reports an error.
/// Always wraps the original SDK exception to prevent provider-specific types from leaking.
/// </summary>
public sealed class StorageProviderException : StorageException
{
    /// <summary>The name of the provider that raised the error.</summary>
    public string? ProviderName { get; }

    /// <summary>The provider-independent error category.</summary>
    public Models.StorageErrorCode ErrorCode { get; }

    /// <inheritdoc />
    public StorageProviderException(
        string message,
        string? providerName = null,
        Exception? innerException = null,
        Models.StorageErrorCode errorCode = Models.StorageErrorCode.ProviderError)
        : base(message, innerException)
    {
        ProviderName = providerName;
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Thrown when the framework is misconfigured — e.g. an unknown policy key, missing profile,
/// or no default provider set when multiple providers are registered.
/// </summary>
public sealed class StorageConfigurationException : StorageException
{
    /// <inheritdoc />
    public StorageConfigurationException(string message) : base(message) { }
}

/// <summary>
/// Thrown when a naming strategy fails to generate a valid object key.
/// </summary>
public sealed class StorageNamingException : StorageException
{
    /// <inheritdoc />
    public StorageNamingException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
