using System.IO.Pipelines;

namespace StorageFlow.Provider.Minio;

internal sealed class MinioPipeStream : Stream
{
    private readonly PipeReader _reader;
    private readonly Stream _inner;
    private readonly Task _transfer;
    private readonly CancellationTokenSource _transferCancellation;
    private bool _disposed;

    internal MinioPipeStream(
        PipeReader reader,
        Task transfer,
        CancellationTokenSource transferCancellation)
    {
        _reader = reader;
        _inner = reader.AsStream(leaveOpen: true);
        _transfer = transfer;
        _transferCancellation = transferCancellation;
    }

    public override bool CanRead => !_disposed;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) =>
        _inner.Read(buffer, offset, count);
    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default) =>
        _inner.ReadAsync(buffer, cancellationToken);
    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException();
    public override void SetLength(long value) =>
        throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _transferCancellation.Cancel();
            _inner.Dispose();
            _reader.Complete();
            _transferCancellation.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _transferCancellation.Cancel();
        await _inner.DisposeAsync();
        await _reader.CompleteAsync();

        try
        {
            await _transfer;
        }
        catch (OperationCanceledException)
        {
        }

        _transferCancellation.Dispose();
        GC.SuppressFinalize(this);
    }
}
