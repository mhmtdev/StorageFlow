namespace StorageFlow.Core.Pipeline;

/// <summary>
/// Replays a small prefix read from a non-seekable stream without taking ownership
/// of the caller's underlying stream.
/// </summary>
internal sealed class PrefixReplayStream : Stream
{
    private readonly Stream _source;
    private readonly byte[] _prefix;
    private int _prefixPosition;
    private bool _sourceWasConsumed;

    private PrefixReplayStream(Stream source, byte[] prefix)
    {
        _source = source;
        _prefix = prefix;
    }

    internal static async Task<PrefixReplayStream> CreateAsync(
        Stream source,
        int prefixLength,
        CancellationToken cancellationToken)
    {
        var prefix = new byte[prefixLength];
        var bytesRead = 0;
        while (bytesRead < prefix.Length)
        {
            var read = await source.ReadAsync(
                prefix.AsMemory(bytesRead, prefix.Length - bytesRead),
                cancellationToken);
            if (read == 0)
                break;

            bytesRead += read;
        }

        if (bytesRead != prefix.Length)
            Array.Resize(ref prefix, bytesRead);

        return new PrefixReplayStream(source, prefix);
    }

    internal bool TryResetPrefix()
    {
        if (_sourceWasConsumed)
            return false;

        _prefixPosition = 0;
        return true;
    }

    public override bool CanRead => _source.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        var prefixRead = ReadPrefix(buffer);
        if (prefixRead == buffer.Length)
            return prefixRead;

        var sourceRead = _source.Read(buffer[prefixRead..]);
        _sourceWasConsumed |= sourceRead > 0;
        return prefixRead + sourceRead;
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        var prefixRead = ReadPrefix(buffer.Span);
        if (prefixRead == buffer.Length)
            return prefixRead;

        var sourceRead = await _source.ReadAsync(buffer[prefixRead..], cancellationToken);
        _sourceWasConsumed |= sourceRead > 0;
        return prefixRead + sourceRead;
    }

    public override void Flush()
    {
    }

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException();

    public override void SetLength(long value) =>
        throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    private int ReadPrefix(Span<byte> destination)
    {
        var available = _prefix.Length - _prefixPosition;
        var count = Math.Min(available, destination.Length);
        if (count == 0)
            return 0;

        _prefix.AsSpan(_prefixPosition, count).CopyTo(destination);
        _prefixPosition += count;
        return count;
    }
}
