using System.Net;
using Amazon.S3.Model;
using StorageFlow.Abstractions.Models;
using StorageFlow.Tests.Cloud.Infrastructure;

namespace StorageFlow.Tests.Cloud.S3;

[Collection(AwsCloudCollection.Name)]
public class AwsS3SmokeTests(AwsCloudFixture fixture)
{
    [AwsCloudFact]
    public async Task FullObjectLifecycle_UsesDefaultCredentialChain()
    {
        byte[] bytes = [0x25, 0x50, 0x44, 0x46, 1, 2, 3, 4];
        var upload = await fixture.Storage
            .CacheControl("private, max-age=0")
            .ContentDisposition("attachment")
            .Metadata("source", "cloud-test")
            .FromStream(new MemoryStream(bytes), "report.pdf", "application/pdf", bytes.Length)
            .UploadAsync(fixture.Bucket);

        Assert.True(upload.IsSuccess, upload.Error?.Message);
        fixture.Track(upload.Value!.ObjectKey);
        Assert.StartsWith(fixture.Prefix, upload.Value.ObjectKey);
        Assert.False(string.IsNullOrWhiteSpace(upload.Value.ETag));

        var exists = await fixture.Storage
            .Object(fixture.Bucket, upload.Value.ObjectKey)
            .ExistsAsync();
        var download = await fixture.Storage
            .Object(fixture.Bucket, upload.Value.ObjectKey)
            .DownloadAsync();

        Assert.True(exists.Value);
        Assert.True(download.IsSuccess, download.Error?.Message);
        Assert.Equal("application/pdf", download.Value!.ContentType);
        Assert.Equal(bytes.Length, download.Value.ContentLength);
        Assert.Equal("cloud-test", download.Value.Metadata["SOURCE"]);
        await using (var content = download.Value.Content)
        {
            using var copy = new MemoryStream();
            await content.CopyToAsync(copy);
            Assert.Equal(bytes, copy.ToArray());
        }

        var metadata = await fixture.ManagementClient.GetObjectMetadataAsync(
            new GetObjectMetadataRequest
            {
                BucketName = fixture.Bucket,
                Key = upload.Value.ObjectKey
            });
        Assert.Equal("private, max-age=0", metadata.Headers.CacheControl);
        Assert.Equal("attachment", metadata.Headers.ContentDisposition);

        var presigned = await fixture.Storage
            .Object(fixture.Bucket, upload.Value.ObjectKey)
            .GetPresignedUrlAsync<AwsCloudFixture.CloudDownloadPolicy>();
        Assert.True(presigned.IsSuccess, presigned.Error?.Message);
        Assert.StartsWith("https://", presigned.Value, StringComparison.OrdinalIgnoreCase);
        using var http = new HttpClient();
        Assert.Equal(bytes, await http.GetByteArrayAsync(presigned.Value));

        var delete = await fixture.Storage
            .Object(fixture.Bucket, upload.Value.ObjectKey)
            .DeleteAsync();
        Assert.True(delete.IsSuccess, delete.Error?.Message);
        fixture.Untrack(upload.Value.ObjectKey);
    }

    [AwsCloudFact]
    public async Task MissingObject_ReturnsObjectNotFound()
    {
        var result = await fixture.Storage
            .Object(fixture.Bucket, $"{fixture.Prefix}/missing-{Guid.NewGuid():N}.bin")
            .DownloadAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(StorageErrorCode.ObjectNotFound, result.Error!.Code);
    }
}
