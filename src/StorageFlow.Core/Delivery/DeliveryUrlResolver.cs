using System.Buffers;
using System.Text;
using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Abstractions.Models;

namespace StorageFlow.Core.Delivery;

/// <summary>
/// Generates stable public CDN URLs using synchronous allocation-conscious path encoding.
/// </summary>
public sealed class DeliveryUrlResolver : IDeliveryUrlResolver
{
    private const string Hex = "0123456789ABCDEF";

    /// <inheritdoc />
    public StorageResult<ObjectDeliveryUrlResult> Resolve(
        DeliveryUrlPolicy policy,
        string bucket,
        string objectKey)
    {
        if (!TryPrepareBucket(policy, bucket, out var encodedBucket, out var error))
            return StorageResult<ObjectDeliveryUrlResult>.Failure(error!);

        return StorageResult<ObjectDeliveryUrlResult>.Success(
            ResolveItem(policy, encodedBucket, objectKey));
    }

    /// <inheritdoc />
    public StorageResult<IReadOnlyList<ObjectDeliveryUrlResult>> ResolveMany(
        DeliveryUrlPolicy policy,
        string bucket,
        IReadOnlyList<string> objectKeys)
    {
        ArgumentNullException.ThrowIfNull(objectKeys);

        if (!TryPrepareBucket(policy, bucket, out var encodedBucket, out var error))
        {
            return StorageResult<IReadOnlyList<ObjectDeliveryUrlResult>>.Failure(error!);
        }

        var results = new ObjectDeliveryUrlResult[objectKeys.Count];
        for (var index = 0; index < objectKeys.Count; index++)
            results[index] = ResolveItem(policy, encodedBucket, objectKeys[index]);

        return StorageResult<IReadOnlyList<ObjectDeliveryUrlResult>>.Success(results);
    }

    private static ObjectDeliveryUrlResult ResolveItem(
        DeliveryUrlPolicy policy,
        string encodedBucket,
        string? objectKey)
    {
        if (!TryValidateObjectKey(objectKey, out var validationError) ||
            !TryGetEncodedLength(objectKey.AsSpan(), preserveSlash: true, out var encodedKeyLength))
        {
            return FailedItem(
                objectKey ?? string.Empty,
                validationError ?? "Object key contains invalid Unicode data.");
        }

        var prefix = policy.EncodedPathPrefix;
        var totalLength =
            policy.BaseUrl.Length +
            1 +
            (prefix.Length == 0 ? 0 : prefix.Length + 1) +
            (encodedBucket.Length == 0 ? 0 : encodedBucket.Length + 1) +
            encodedKeyLength;

        var state = new UrlState(
            policy.BaseUrl,
            prefix,
            encodedBucket,
            objectKey!);

        var url = string.Create(totalLength, state, static (destination, value) =>
        {
            var offset = 0;
            value.BaseUrl.AsSpan().CopyTo(destination);
            offset += value.BaseUrl.Length;
            destination[offset++] = '/';

            if (value.Prefix.Length != 0)
            {
                value.Prefix.AsSpan().CopyTo(destination[offset..]);
                offset += value.Prefix.Length;
                destination[offset++] = '/';
            }

            if (value.EncodedBucket.Length != 0)
            {
                value.EncodedBucket.AsSpan().CopyTo(destination[offset..]);
                offset += value.EncodedBucket.Length;
                destination[offset++] = '/';
            }

            WriteEncoded(
                value.ObjectKey.AsSpan(),
                destination[offset..],
                preserveSlash: true);
        });

        return new ObjectDeliveryUrlResult
        {
            ObjectKey = objectKey!,
            Url = url
        };
    }

    private static bool TryPrepareBucket(
        DeliveryUrlPolicy policy,
        string bucket,
        out string encodedBucket,
        out StorageError? error)
    {
        encodedBucket = string.Empty;
        error = null;

        if (!policy.IncludesBucket)
            return true;

        if (string.IsNullOrWhiteSpace(bucket) ||
            bucket.Contains('/') ||
            bucket.Contains('\\') ||
            bucket is "." or "..")
        {
            error = StorageError.Create(
                StorageErrorCode.Unknown,
                "The bucket must be a non-empty relative path segment when IncludeBucket() is enabled.");
            return false;
        }

        if (!TryGetEncodedLength(bucket.AsSpan(), preserveSlash: false, out var encodedLength))
        {
            error = StorageError.Create(
                StorageErrorCode.Unknown,
                "The bucket contains invalid Unicode data.");
            return false;
        }

        encodedBucket = string.Create(
            encodedLength,
            bucket,
            static (destination, value) =>
                WriteEncoded(value.AsSpan(), destination, preserveSlash: false));
        return true;
    }

    private static bool TryValidateObjectKey(string? objectKey, out string? error)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            error = "Object key cannot be empty.";
            return false;
        }

        if (objectKey.Contains('\\'))
        {
            error = "Object key must use forward slashes.";
            return false;
        }

        if (objectKey.StartsWith('/') ||
            objectKey.Length >= 3 &&
            char.IsLetter(objectKey[0]) &&
            objectKey[1] == ':' &&
            objectKey[2] == '/')
        {
            error = "Object key must be relative.";
            return false;
        }

        var segmentStart = 0;
        for (var index = 0; index <= objectKey.Length; index++)
        {
            if (index != objectKey.Length && objectKey[index] != '/')
                continue;

            var length = index - segmentStart;
            if (length is 1 or 2)
            {
                var segment = objectKey.AsSpan(segmentStart, length);
                if (segment.SequenceEqual(".") || segment.SequenceEqual(".."))
                {
                    error = "Object key cannot contain '.' or '..' path segments.";
                    return false;
                }
            }

            segmentStart = index + 1;
        }

        error = null;
        return true;
    }

    private static ObjectDeliveryUrlResult FailedItem(string objectKey, string message) =>
        new()
        {
            ObjectKey = objectKey,
            Error = StorageError.Create(StorageErrorCode.Unknown, message)
        };

    private static bool TryGetEncodedLength(
        ReadOnlySpan<char> value,
        bool preserveSlash,
        out int length)
    {
        length = 0;
        var remaining = value;

        while (!remaining.IsEmpty)
        {
            var status = Rune.DecodeFromUtf16(remaining, out var rune, out var consumed);
            if (status != OperationStatus.Done)
                return false;

            if (rune.IsAscii)
            {
                var character = (char)rune.Value;
                length += IsUnreserved(character) || preserveSlash && character == '/'
                    ? 1
                    : 3;
            }
            else
            {
                length += rune.Utf8SequenceLength * 3;
            }

            remaining = remaining[consumed..];
        }

        return true;
    }

    private static void WriteEncoded(
        ReadOnlySpan<char> value,
        Span<char> destination,
        bool preserveSlash)
    {
        var offset = 0;
        var remaining = value;
        Span<byte> utf8 = stackalloc byte[4];

        while (!remaining.IsEmpty)
        {
            Rune.DecodeFromUtf16(remaining, out var rune, out var consumed);
            if (rune.IsAscii)
            {
                var character = (char)rune.Value;
                if (IsUnreserved(character) || preserveSlash && character == '/')
                {
                    destination[offset++] = character;
                }
                else
                {
                    WritePercentEncoded((byte)character, destination, ref offset);
                }
            }
            else
            {
                var byteCount = rune.EncodeToUtf8(utf8);
                for (var index = 0; index < byteCount; index++)
                    WritePercentEncoded(utf8[index], destination, ref offset);
            }

            remaining = remaining[consumed..];
        }
    }

    private static void WritePercentEncoded(
        byte value,
        Span<char> destination,
        ref int offset)
    {
        destination[offset++] = '%';
        destination[offset++] = Hex[value >> 4];
        destination[offset++] = Hex[value & 0x0F];
    }

    private static bool IsUnreserved(char value) =>
        value is >= 'A' and <= 'Z' or
            >= 'a' and <= 'z' or
            >= '0' and <= '9' or
            '-' or '.' or '_' or '~';

    private readonly record struct UrlState(
        string BaseUrl,
        string Prefix,
        string EncodedBucket,
        string ObjectKey);
}
