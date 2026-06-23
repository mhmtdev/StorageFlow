using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using StorageFlow.Abstractions.Interfaces;
using StorageFlow.Abstractions.Models;
using StorageFlow.Sample.Api.Policies;

namespace StorageFlow.Tests.Component.Sample;

public class SampleEndpointTests
{
    [Fact]
    public async Task Root_ReturnsDocumentedEndpointList()
    {
        await using var factory = CreateFactory(Substitute.For<IStorageService>());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("POST /api/storage/upload", json);
        Assert.Contains("GET /api/storage/download/{objectKey}", json);
    }

    [Fact]
    public async Task Upload_ForwardsMultipartFileAndReturnsUploadResult()
    {
        var storage = Substitute.For<IStorageService>();
        var operation = Substitute.For<IStorageOperationBuilder>();
        storage.Validation<DocumentsPolicy>().Returns(operation);
        operation.CacheControl(Arg.Any<string>()).Returns(operation);
        operation.ContentDisposition(Arg.Any<string>()).Returns(operation);
        operation.Metadata(Arg.Any<string>(), Arg.Any<string>()).Returns(operation);
        operation.FromStream(
                Arg.Any<Stream>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<long?>())
            .Returns(operation);
        operation.UploadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(StorageResult<UploadResult>.Success(new UploadResult
            {
                ObjectKey = "objects/document.pdf",
                Bucket = "sample-minio",
                ProviderName = "minio",
                UploadedAt = DateTimeOffset.UtcNow
            }));
        await using var factory = CreateFactory(storage);
        using var client = factory.CreateClient();
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([0x25, 0x50, 0x44, 0x46]), "file", "document.pdf");

        var response = await client.PostAsync("/api/storage/upload", form);
        var result = await response.Content.ReadFromJsonAsync<UploadResult>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("objects/document.pdf", result!.ObjectKey);
    }

    [Theory]
    [InlineData("/api/storage/exists/folder/file.pdf")]
    [InlineData("/api/storage/presigned/folder/file.pdf")]
    [InlineData("/api/storage/delivery/folder/file.pdf")]
    public async Task ReadEndpoints_AreMapped(string path)
    {
        var storage = Substitute.For<IStorageService>();
        var objects = Substitute.For<IStorageObjectBuilder>();
        storage.Object("sample-minio", "folder/file.pdf").Returns(objects);
        objects.ExistsAsync(Arg.Any<CancellationToken>())
            .Returns(StorageResult<bool>.Success(true));
        objects.GetPresignedUrlAsync<DownloadPolicy>(Arg.Any<CancellationToken>())
            .Returns(StorageResult<string>.Success("https://storage.example/signed"));
        objects.GetDeliveryUrl<PublicAssetDelivery>()
            .Returns(StorageResult<ObjectDeliveryUrlResult>.Success(
                new ObjectDeliveryUrlResult
                {
                    ObjectKey = "folder/file.pdf",
                    Url = "https://cdn.example/folder/file.pdf"
                }));
        await using var factory = CreateFactory(storage);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DownloadAndDeleteEndpoints_AreMapped()
    {
        var storage = Substitute.For<IStorageService>();
        var objects = Substitute.For<IStorageObjectBuilder>();
        storage.Object("sample-minio", "folder/file.pdf").Returns(objects);
        objects.DownloadAsync(Arg.Any<CancellationToken>())
            .Returns(StorageResult<DownloadResult>.Success(new DownloadResult
            {
                Content = new MemoryStream([1, 2, 3]),
                ContentType = "application/pdf",
                ContentLength = 3
            }));
        objects.DeleteAsync(Arg.Any<CancellationToken>())
            .Returns(StorageResult.Success());
        await using var factory = CreateFactory(storage);
        using var client = factory.CreateClient();

        var download = await client.GetAsync(
            "/api/storage/download/folder/file.pdf");
        var delete = await client.DeleteAsync(
            "/api/storage/folder/file.pdf");

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal([1, 2, 3], await download.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(IStorageService storage) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("StorageFlow:Minio:Enabled", "true");
            builder.UseSetting("StorageFlow:Minio:AccessKey", "test-access");
            builder.UseSetting("StorageFlow:Minio:SecretKey", "test-secret");
            builder.UseSetting("StorageFlow:S3:Enabled", "false");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IStorageService>();
                services.AddSingleton(storage);
            });
        });
}
