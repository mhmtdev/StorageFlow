using System.Text;
using System.Text.RegularExpressions;
using StorageFlow.Abstractions.Interfaces;

namespace StorageFlow.Core.Naming;

/// <summary>
/// Generates SEO-friendly object keys by slugifying the original file name.
/// Non-ASCII characters are transliterated; special characters are replaced with hyphens.
/// A short GUID segment is appended to ensure uniqueness.
/// Example: "My Product Image!.jpg" → "my-product-image-a1b2c3d4.jpg"
/// </summary>
public sealed class SeoNamingStrategy : IFileNamingStrategy
{
    private static readonly Regex NonAlphanumericPattern = new(@"[^a-z0-9]+", RegexOptions.Compiled);
    private static readonly Regex LeadingTrailingHyphens = new(@"^-+|-+$", RegexOptions.Compiled);

    /// <inheritdoc />
    public Task<string> GenerateAsync(FileNamingContext context, CancellationToken cancellationToken = default)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(context.OriginalFileName);
        var slug = Slugify(nameWithoutExt);
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var key = $"{slug}-{uniqueSuffix}{context.Extension}";
        return Task.FromResult(key);
    }

    private static string Slugify(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "file";

        // Normalize to decomposed form then strip diacritics
        var normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        var slug = sb.ToString()
            .Normalize(NormalizationForm.FormC)
            .ToLowerInvariant();

        slug = NonAlphanumericPattern.Replace(slug, "-");
        slug = LeadingTrailingHyphens.Replace(slug, string.Empty);

        return string.IsNullOrEmpty(slug) ? "file" : slug;
    }
}

