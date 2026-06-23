namespace StorageFlow.Sample.Api.Configuration;

public sealed class StorageBackendOptions
{
    public const string SectionName = "StorageFlow";

    public MinioOptions Minio { get; set; } = new();
    public S3Options S3 { get; set; } = new();
    public CdnOptions Cdn { get; set; } = new();

    public string Bucket =>
        Minio.Enabled
            ? Minio.Bucket
            : S3.Bucket;
}

public sealed class MinioOptions
{
    public bool Enabled { get; set; } = true;
    public string Bucket { get; set; } = "sample-minio";
    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public bool UseSsl { get; set; }
    public string Region { get; set; } = "us-east-1";
}

public sealed class S3Options
{
    public bool Enabled { get; set; }
    public string Bucket { get; set; } = "sample-s3";
    public string Region { get; set; } = "eu-central-1";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string? SessionToken { get; set; }
}

public sealed class CdnOptions
{
    public string BaseUrl { get; set; } = "https://cdn.example.com";
    public string PathPrefix { get; set; } = "assets";
    public bool IncludeBucket { get; set; }
}
