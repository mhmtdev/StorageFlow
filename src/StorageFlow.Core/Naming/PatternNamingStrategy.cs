using System.Text.RegularExpressions;
using StorageFlow.Abstractions.Exceptions;
using StorageFlow.Abstractions.Interfaces;

namespace StorageFlow.Core.Naming;

/// <summary>
/// Generates object keys from a user-defined pattern string.
/// Supported tokens:
/// <list type="table">
///   <item><term>{yyyy}</term><description>4-digit year</description></item>
///   <item><term>{MM}</term><description>2-digit month</description></item>
///   <item><term>{dd}</term><description>2-digit day</description></item>
///   <item><term>{guid}</term><description>Short 8-character GUID segment</description></item>
///   <item><term>{slug}</term><description>Slugified original file name (without extension)</description></item>
///   <item><term>{ext}</term><description>File extension including the leading dot</description></item>
///   <item><term>{timestamp}</term><description>Unix timestamp in seconds</description></item>
/// </list>
/// Example pattern: <c>{yyyy}/{MM}/{slug}-{guid}{ext}</c>
/// → <c>2026/06/my-product-image-a1b2c3d4.jpg</c>
/// </summary>
public sealed class PatternNamingStrategy : IFileNamingStrategy
{
    private static readonly Regex TokenPattern = new(@"\{(\w+)\}", RegexOptions.Compiled);
    private static readonly Regex SlugNonAlpha = new(@"[^a-z0-9]+", RegexOptions.Compiled);

    private readonly string? _defaultPattern;

    /// <param name="defaultPattern">
    /// Optional default pattern used when the naming policy does not supply one.
    /// </param>
    public PatternNamingStrategy(string? defaultPattern = null)
    {
        _defaultPattern = defaultPattern;
    }

    /// <inheritdoc />
    public Task<string> GenerateAsync(FileNamingContext context, CancellationToken cancellationToken = default)
    {
        var pattern = context.Pattern ?? _defaultPattern;

        if (string.IsNullOrWhiteSpace(pattern))
            throw new StorageNamingException(
                "PatternNamingStrategy requires a pattern. " +
                "Set it on the naming policy or pass a default to the constructor.");

        var now = context.UploadedAt;
        var slug = Slugify(Path.GetFileNameWithoutExtension(context.OriginalFileName));
        var guidSegment = Guid.NewGuid().ToString("N")[..8];

        var result = TokenPattern.Replace(pattern, match =>
        {
            return match.Groups[1].Value switch
            {
                "yyyy"      => now.Year.ToString("D4"),
                "MM"        => now.Month.ToString("D2"),
                "dd"        => now.Day.ToString("D2"),
                "guid"      => guidSegment,
                "slug"      => slug,
                "ext"       => context.Extension,
                "timestamp" => now.ToUnixTimeSeconds().ToString(),
                var unknown => throw new StorageNamingException($"Unknown pattern token '{{{unknown}}}'.")
            };
        });

        return Task.FromResult(result);
    }

    private static string Slugify(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "file";
        var lower = input.ToLowerInvariant();
        var slug = SlugNonAlpha.Replace(lower, "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "file" : slug;
    }
}
