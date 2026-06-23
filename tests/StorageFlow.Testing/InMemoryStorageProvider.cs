using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using StorageFlow.Abstractions.Exceptions;
using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Abstractions.Models;

namespace StorageFlow.Testing;

public sealed class InMemoryStorageProvider : IStorageProvider
{
    private readonly ConcurrentDictionary<string, InMemoryObject> _store = new();

    public string ProviderName => "memory";

    public Task<UploadResult> UploadAsync(
        string bucket,
        string objectKey,
        Stream content,
        string? contentType = null,
        long? contentLength = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        UploadHeaders? headers = null,
        CancellationToken cancellationToken = default)
    {
        var key = BuildKey(bucket, objectKey);
        var bytes = ReadAllBytes(content);

        var uploadedAt = DateTimeOffset.UtcNow;
        var eTag = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        _store[key] = new InMemoryObject(
            bytes,
            contentType,
            metadata is null
                ? null
                : new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase),
            headers,
            eTag,
            uploadedAt);

        return Task.FromResult(new UploadResult
        {
            ObjectKey = objectKey,
            Bucket = bucket,
            ProviderName = ProviderName,
            ContentType = contentType,
            SizeBytes = bytes.Length,
            ETag = eTag,
            UploadedAt = uploadedAt
        });
    }

    public Task<DownloadResult> DownloadAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        var key = BuildKey(bucket, objectKey);

        if (!_store.TryGetValue(key, out var obj))
        {
            throw new StorageProviderException(
                $"Object '{objectKey}' not found in bucket '{bucket}'.",
                ProviderName,
                errorCode: StorageErrorCode.ObjectNotFound);
        }

        return Task.FromResult(new DownloadResult
        {
            Content = new MemoryStream(obj.Data, writable: false),
            ContentType = obj.ContentType,
            ContentLength = obj.Data.Length,
            ETag = obj.ETag,
            LastModified = obj.LastModified,
            Metadata = new ReadOnlyDictionary<string, string>(
                obj.Metadata is null
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(
                        obj.Metadata,
                        StringComparer.OrdinalIgnoreCase))
        });
    }

    public Task DeleteAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        _store.TryRemove(BuildKey(bucket, objectKey), out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.ContainsKey(BuildKey(bucket, objectKey)));

    public Task<string> GetPresignedUrlAsync(
        string bucket,
        string objectKey,
        TimeSpan expiration,
        HttpMethod httpMethod,
        CancellationToken cancellationToken = default)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(expiration).ToUnixTimeSeconds();
        return Task.FromResult(
            $"https://memory.local/{bucket}/{objectKey}?expires={expiresAt}");
    }

    public IEnumerable<string> GetAllKeys() => _store.Keys;

    public IReadOnlyDictionary<string, string>? GetMetadata(string bucket, string objectKey) =>
        _store.TryGetValue(BuildKey(bucket, objectKey), out var obj)
            ? obj.Metadata
            : null;

    public UploadHeaders? GetHeaders(string bucket, string objectKey) =>
        _store.TryGetValue(BuildKey(bucket, objectKey), out var obj)
            ? obj.Headers
            : null;

    public void Clear() => _store.Clear();

    private static string BuildKey(string bucket, string objectKey) =>
        $"{bucket.ToLowerInvariant()}/{objectKey}";

    private static byte[] ReadAllBytes(Stream stream)
    {
        if (stream is MemoryStream memoryStream)
            return memoryStream.ToArray();

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private sealed record InMemoryObject(
        byte[] Data,
        string? ContentType,
        IReadOnlyDictionary<string, string>? Metadata,
        UploadHeaders? Headers,
        string ETag,
        DateTimeOffset LastModified);
}
