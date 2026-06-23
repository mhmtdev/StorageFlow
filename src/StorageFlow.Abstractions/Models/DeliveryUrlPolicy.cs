using System.Net;
using StorageFlow.Abstractions.Exceptions;

namespace StorageFlow.Abstractions.Models;

/// <summary>
/// Defines how stable public CDN delivery URLs are generated.
/// </summary>
public class DeliveryUrlPolicy
{
    private string? _baseUrl;
    private string? _pathPrefix;
    private bool _isFrozen;

    /// <summary>Normalized CDN base URL without a trailing slash.</summary>
    public string BaseUrl { get; private set; } = string.Empty;

    /// <summary>Normalized optional path prefix without leading or trailing slashes.</summary>
    public string PathPrefix { get; private set; } = string.Empty;

    /// <summary>Whether the storage bucket is included in the generated URL path.</summary>
    public bool IncludesBucket { get; private set; }

    internal string EncodedPathPrefix { get; private set; } = string.Empty;

    /// <summary>Sets the public CDN base URL.</summary>
    public DeliveryUrlPolicy UseCdn(string baseUrl)
    {
        EnsureMutable();
        _baseUrl = baseUrl;
        return this;
    }

    /// <summary>Sets an optional path prefix placed before the bucket and object key.</summary>
    public DeliveryUrlPolicy WithPathPrefix(string pathPrefix)
    {
        EnsureMutable();
        _pathPrefix = pathPrefix;
        return this;
    }

    /// <summary>Includes the storage bucket in the generated URL path.</summary>
    public DeliveryUrlPolicy IncludeBucket(bool include = true)
    {
        EnsureMutable();
        IncludesBucket = include;
        return this;
    }

    internal void ValidateAndNormalize()
    {
        if (string.IsNullOrWhiteSpace(_baseUrl) ||
            !Uri.TryCreate(_baseUrl, UriKind.Absolute, out var uri))
        {
            throw new StorageConfigurationException(
                "A delivery URL policy must define a valid absolute CDN base URL.");
        }

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new StorageConfigurationException(
                "A CDN base URL cannot contain a query string or fragment.");
        }

        var isHttps = uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        var isLocalHttp =
            uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            IsLocalHost(uri.Host);

        if (!isHttps && !isLocalHttp)
        {
            throw new StorageConfigurationException(
                "A CDN base URL must use HTTPS. HTTP is allowed only for localhost or loopback addresses.");
        }

        var basePath = uri.AbsolutePath.TrimEnd('/');
        BaseUrl = uri.GetLeftPart(UriPartial.Authority) + basePath;

        if (string.IsNullOrWhiteSpace(_pathPrefix))
        {
            PathPrefix = string.Empty;
            EncodedPathPrefix = string.Empty;
            _isFrozen = true;
            return;
        }

        var prefix = _pathPrefix.Trim('/');
        ValidateRelativePath(prefix, "CDN path prefix");
        PathPrefix = prefix;
        EncodedPathPrefix = EncodePath(prefix);
        _isFrozen = true;
    }

    private void EnsureMutable()
    {
        if (_isFrozen)
        {
            throw new StorageConfigurationException(
                "A registered delivery URL policy is immutable.");
        }
    }

    private static bool IsLocalHost(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
        host.Equals("::1", StringComparison.OrdinalIgnoreCase) ||
        IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);

    private static void ValidateRelativePath(string value, string description)
    {
        if (value.Length == 0)
            return;

        if (value.Contains('\\') || value.StartsWith('/'))
            throw new StorageConfigurationException(
                $"{description} must be a relative path using forward slashes.");

        var segmentStart = 0;
        for (var index = 0; index <= value.Length; index++)
        {
            if (index != value.Length && value[index] != '/')
                continue;

            var length = index - segmentStart;
            if (length is 1 or 2)
            {
                var segment = value.AsSpan(segmentStart, length);
                if (segment.SequenceEqual(".") || segment.SequenceEqual(".."))
                {
                    throw new StorageConfigurationException(
                        $"{description} cannot contain '.' or '..' path segments.");
                }
            }

            segmentStart = index + 1;
        }
    }

    private static string EncodePath(string path)
    {
        var segments = path.Split('/');
        for (var index = 0; index < segments.Length; index++)
            segments[index] = Uri.EscapeDataString(segments[index]);
        return string.Join('/', segments);
    }
}
