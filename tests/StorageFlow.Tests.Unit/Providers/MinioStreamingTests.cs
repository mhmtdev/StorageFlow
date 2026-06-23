using System.IO.Pipelines;
using StorageFlow.Provider.Minio;

namespace StorageFlow.Tests.Unit.Providers;

public class MinioStreamingTests
{
    [Fact]
    public async Task PipeStream_ReadsIncrementallyWithoutSeekOrBufferOwnership()
    {
        var pipe = CreateBoundedPipe();
        using var cancellation = new CancellationTokenSource();
        var expected = new byte[1024 * 1024];
        Random.Shared.NextBytes(expected);
        var transfer = ProduceAsync(pipe.Writer, expected, cancellation.Token);
        await using var stream = new MinioPipeStream(
            pipe.Reader,
            transfer,
            cancellation);
        using var destination = new MemoryStream();

        await stream.CopyToAsync(destination);

        Assert.False(stream.CanSeek);
        Assert.Equal(expected, destination.ToArray());
    }

    [Fact]
    public async Task PipeStream_PropagatesProducerFailureDuringRead()
    {
        var pipe = CreateBoundedPipe();
        using var cancellation = new CancellationTokenSource();
        var failure = new IOException("transfer failed");
        var transfer = pipe.Writer.CompleteAsync(failure).AsTask();
        await using var stream = new MinioPipeStream(
            pipe.Reader,
            transfer,
            cancellation);

        var exception = await Assert.ThrowsAsync<IOException>(
            async () => await stream.ReadExactlyAsync(new byte[1]));

        Assert.Equal(failure.Message, exception.Message);
    }

    [Fact]
    public async Task PipeStream_DisposeCancelsAndWaitsForProducer()
    {
        var pipe = CreateBoundedPipe();
        var cancellation = new CancellationTokenSource();
        var transfer = WaitForCancellationAsync(cancellation.Token);
        var stream = new MinioPipeStream(
            pipe.Reader,
            transfer,
            cancellation);

        await stream.DisposeAsync();

        Assert.True(cancellation.IsCancellationRequested);
        Assert.True(transfer.IsCompleted);
    }

    private static Pipe CreateBoundedPipe() =>
        new(new PipeOptions(
            pauseWriterThreshold: 256 * 1024,
            resumeWriterThreshold: 128 * 1024,
            useSynchronizationContext: false));

    private static async Task ProduceAsync(
        PipeWriter writer,
        byte[] content,
        CancellationToken cancellationToken)
    {
        Exception? failure = null;
        try
        {
            const int chunkSize = 32 * 1024;
            for (var offset = 0; offset < content.Length; offset += chunkSize)
            {
                var length = Math.Min(chunkSize, content.Length - offset);
                content.AsSpan(offset, length).CopyTo(writer.GetSpan(length));
                writer.Advance(length);
                await writer.FlushAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            await writer.CompleteAsync(failure);
        }
    }

    private static async Task WaitForCancellationAsync(
        CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }
}
