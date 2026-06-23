using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Abstractions.Models;

namespace StorageFlow.Core.Validation;

/// <summary>
/// Validates the file's magic number (binary signature) against known signatures for the declared extension.
/// Prevents renamed malicious files from bypassing extension/MIME checks.
/// Runs last in the pipeline (Order = 40) because it requires reading bytes from the stream.
/// </summary>
public sealed class FileSignatureValidator : IFileValidator
{
    private readonly ValidationPolicy _policy;

    /// <inheritdoc />
    public int Order => 40;

    // ── Known signatures ─────────────────────────────────────────────────────
    // Key: lowercase extension. Value: list of accepted byte-prefix patterns (null = wildcard byte).

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<byte?[]>> KnownSignatures =
        new Dictionary<string, IReadOnlyList<byte?[]>>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"]  = [[0xFF, 0xD8, 0xFF]],
            [".jpeg"] = [[0xFF, 0xD8, 0xFF]],
            [".png"]  = [[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]],
            [".pdf"]  = [[0x25, 0x50, 0x44, 0x46]],
            [".zip"]  = [[0x50, 0x4B, 0x03, 0x04], [0x50, 0x4B, 0x05, 0x06], [0x50, 0x4B, 0x07, 0x08]],
            [".mp3"]  =
            [
                [0xFF, 0xFB],
                [0xFF, 0xF3],
                [0xFF, 0xF2],
                [0x49, 0x44, 0x33]   // ID3 tag
            ],
            [".mp4"]  =
            [
                // ftyp box: offset 4–7 = "ftyp" (0x66 0x74 0x79 0x70), first 4 bytes are box size (variable)
                [null, null, null, null, 0x66, 0x74, 0x79, 0x70]
            ]
        };

    internal static int MaximumSignatureLength { get; } =
        KnownSignatures.Values.SelectMany(signatures => signatures).Max(signature => signature.Length);

    /// <param name="policy">The validation policy to enforce.</param>
    public FileSignatureValidator(ValidationPolicy policy)
    {
        _policy = policy;
    }

    /// <inheritdoc />
    public async Task<ValidationResult> ValidateAsync(
        FileValidationContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_policy.RequireValidSignature)
            return ValidationResult.Success();

        var ext = Path.GetExtension(context.FileName);

        if (!KnownSignatures.TryGetValue(ext, out var signatures))
            return ValidationResult.Success(); // unknown extension — skip signature check

        // Read enough bytes to cover the longest signature
        var maxLen = signatures.Max(s => s.Length);
        var buffer = new byte[maxLen];

        var originalPosition = context.Content.CanSeek ? context.Content.Position : -1L;

        if (context.Content.CanSeek)
            context.Content.Seek(0, SeekOrigin.Begin);

        var bytesRead = await context.Content.ReadAsync(buffer.AsMemory(0, maxLen), cancellationToken);

        // Reset stream position after reading
        if (context.Content.CanSeek && originalPosition >= 0)
            context.Content.Seek(originalPosition, SeekOrigin.Begin);

        foreach (var sig in signatures)
        {
            if (MatchesSignature(buffer, bytesRead, sig))
                return ValidationResult.Success();
        }

        return ValidationResult.Failure(
            $"File signature does not match the expected format for extension '{ext}'. " +
            "The file may be corrupted or renamed.");
    }

    private static bool MatchesSignature(byte[] buffer, int bytesRead, byte?[] signature)
    {
        if (bytesRead < signature.Length)
            return false;

        for (var i = 0; i < signature.Length; i++)
        {
            if (signature[i] is byte expected && buffer[i] != expected)
                return false;
        }

        return true;
    }
}
