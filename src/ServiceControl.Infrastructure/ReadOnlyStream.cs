namespace ServiceControl.Infrastructure;

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public sealed class ReadOnlyStream(ReadOnlyMemory<byte> memory) : Stream
{
    int position = 0;

    public override long Seek(long offset, SeekOrigin origin)
    {
        long index = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => position + offset,
            SeekOrigin.End => memory.Length + offset,
            _ => ThrowArgumentExceptionSeekMode(nameof(origin))
        };

        position = unchecked((int)index);

        return index;
    }

    [DoesNotReturn]
    static long ThrowArgumentExceptionSeekMode(string paramName)
        => throw new ArgumentException("The input seek mode is not valid.", paramName);

    public override void CopyTo(Stream destination, int bufferSize)
    {
        ReadOnlySpan<byte> source = memory.Span[position..];

        position += source.Length;

        destination.Write(source);
    }

    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        try
        {
            CopyTo(destination, bufferSize);

            return Task.CompletedTask;
        }
        catch (OperationCanceledException e) when (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(e.CancellationToken);
        }
        catch (Exception e)
        {
            return Task.FromException(e);
        }
    }

    public override int ReadByte()
    {
        if (position == memory.Length)
        {
            return -1;
        }

        return memory.Span[position++];
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesToCopy = Math.Min(count, memory.Length - position);

        var destination = buffer.AsSpan().Slice(offset, bytesToCopy);
        var source = memory.Span.Slice(position, bytesToCopy);

        source.CopyTo(destination);

        position += bytesToCopy;

        return bytesToCopy;
    }

    public override int Read(Span<byte> buffer)
    {
        var bytesToCopy = Math.Min(memory.Length - position, buffer.Length);
        if (bytesToCopy <= 0)
        {
            return 0;
        }

        var source = memory.Span.Slice(position, bytesToCopy);
        source.CopyTo(buffer);

        position += bytesToCopy;
        return bytesToCopy;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<int>(cancellationToken);
        }

        try
        {
            int result = Read(buffer.AsSpan());

            return Task.FromResult(result);
        }
        catch (OperationCanceledException e) when (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<int>(e.CancellationToken);
        }
        catch (Exception e)
        {
            return Task.FromException<int>(e);
        }
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new ValueTask<int>(Task.FromCanceled<int>(cancellationToken));
        }

        try
        {
            int result = Read(buffer.Span);

            return new ValueTask<int>(result);
        }
        catch (OperationCanceledException e) when (cancellationToken.IsCancellationRequested)
        {
            return new ValueTask<int>(Task.FromCanceled<int>(e.CancellationToken));
        }
        catch (Exception e)
        {
            return new ValueTask<int>(Task.FromException<int>(e));
        }
    }

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override void WriteByte(byte value) => throw new NotSupportedException();

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public override void Write(ReadOnlySpan<byte> buffer) => throw new NotSupportedException();

    public override Task FlushAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public override void Flush() => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => memory.Length;
    public override long Position { get => position; set => position = unchecked((int)value); }
}