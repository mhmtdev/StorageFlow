namespace StorageFlow.Core.Pipeline;

internal sealed class UploadRequest
{
    internal Stream? Content { get; set; }
    internal string? FileName { get; set; }
    internal string? ContentType { get; set; }
    internal long? ContentLength { get; set; }
    internal string? CacheControl { get; set; }
    internal string? ContentDisposition { get; set; }
    internal Type? ValidationPolicyKey { get; set; }
    internal Type? NamingPolicyKey { get; set; }
    internal Type? PresignedUrlPolicyKey { get; set; }
    internal Dictionary<string, string> Metadata { get; } =
        new(StringComparer.OrdinalIgnoreCase);
}
