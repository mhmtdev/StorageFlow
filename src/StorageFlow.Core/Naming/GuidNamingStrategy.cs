using StorageFlow.Abstractions.Interfaces;

namespace StorageFlow.Core.Naming;

/// <summary>
/// Generates object keys using a standard GUID (e.g. "550e8400-e29b-41d4-a716-446655440000.jpg").
/// </summary>
public sealed class GuidNamingStrategy : IFileNamingStrategy
{
    /// <inheritdoc />
    public Task<string> GenerateAsync(FileNamingContext context, CancellationToken cancellationToken = default)
    {
        var key = $"{Guid.NewGuid()}{context.Extension}";
        return Task.FromResult(key);
    }
}

