using Amazon.S3;
using Amazon.S3.Model;

namespace StorageFlow.Tests.Integration.Infrastructure;

internal sealed class S3BucketManager(IAmazonS3 client)
{
    internal async Task CreateWhenReadyAsync(
        string bucket,
        CancellationToken cancellationToken = default)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            try
            {
                await client.PutBucketAsync(new PutBucketRequest
                {
                    BucketName = bucket,
                    UseClientRegion = true
                }, cancellationToken);
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }
        }

        throw new InvalidOperationException(
            $"Object storage container did not become ready for bucket '{bucket}'.",
            lastError);
    }
}
