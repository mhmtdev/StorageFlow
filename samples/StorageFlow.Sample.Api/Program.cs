using StorageFlow.Sample.Api.Configuration;
using StorageFlow.Sample.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddStorageFlowSample(builder.Configuration);

var app = builder.Build();
var storageOptions = app.Services.GetRequiredService<StorageBackendOptions>();

app.MapGet("/", () => Results.Ok(new
{
    name = "StorageFlow Minimal Sample API",
    provider = storageOptions.Minio.Enabled ? "MinIO" : "AWS S3",
    bucket = storageOptions.Bucket,
    endpoints = new[]
    {
        "POST /api/storage/upload",
        "GET /api/storage/download/{objectKey}",
        "DELETE /api/storage/{objectKey}",
        "GET /api/storage/exists/{objectKey}",
        "GET /api/storage/presigned/{objectKey}",
        "GET /api/storage/delivery/{objectKey}"
    }
}));

app.MapStorageEndpoints();

app.Run();

public partial class Program;
