using StorageFlow.Abstractions.Exceptions;
using StorageFlow.Abstractions.Interfaces;

namespace StorageFlow.Core.Naming;

/// <summary>
/// Validates generated object keys before they are sent to a storage provider.
/// </summary>
public sealed class ObjectKeyValidator : IObjectKeyValidator
{
    /// <inheritdoc />
    public void Validate(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
            throw new StorageNamingException("A naming strategy generated an empty object key.");

        if (objectKey.Contains('\\'))
            throw new StorageNamingException("Object keys must use forward slashes.");

        var isWindowsAbsolutePath =
            objectKey.Length >= 3 &&
            char.IsLetter(objectKey[0]) &&
            objectKey[1] == ':' &&
            objectKey[2] == '/';

        if (objectKey.StartsWith('/') || isWindowsAbsolutePath)
            throw new StorageNamingException("Object keys must be relative.");

        var segments = objectKey.Split('/');
        if (segments.Any(segment => segment is "." or ".."))
            throw new StorageNamingException("Object keys cannot contain '.' or '..' path segments.");
    }
}
