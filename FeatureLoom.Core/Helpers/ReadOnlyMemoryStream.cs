using System;
using System.IO;

namespace FeatureLoom.Helpers;

#if !NETSTANDARD2_0
/// <summary>
/// A read-only stream implementation backed by a <see cref="ReadOnlyMemory{T}"/>.
/// </summary>
public sealed class ReadOnlyMemoryStream : Stream
{
    private ReadOnlyMemory<byte> _memory;
    private int _position;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReadOnlyMemoryStream"/> class with the specified memory buffer.
    /// </summary>
    /// <param name="memory">The memory buffer to use as the source for the stream.</param>
    public ReadOnlyMemoryStream(ReadOnlyMemory<byte> memory)
    {
        _memory = memory;
        _position = 0;
    }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => true;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => _memory.Length;

    /// <inheritdoc />
    public override long Position
    {
        get => _position;
        set
        {
            if (value < 0 || value > Length)
                throw new ArgumentOutOfRangeException(nameof(value));
            _position = (int)value;
        }
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (offset < 0 || count < 0 || offset + count > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset and count exceed buffer bounds.");

        return Read(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        var remaining = _memory.Length - _position;
        if (remaining <= 0) return 0;

        var toCopy = Math.Min(buffer.Length, remaining);

        _memory.Slice(_position, toCopy).Span.CopyTo(buffer);
        _position += toCopy;
        return toCopy;
    }

    /// <summary>
    /// Sets the position within the current stream.
    /// </summary>
    /// <param name="offset">A byte offset relative to the <paramref name="origin"/> parameter.</param>
    /// <param name="origin">A value of type <see cref="SeekOrigin"/> indicating the reference point used to obtain the new position.</param>
    /// <returns>The new position within the stream.</returns>
    /// <exception cref="IOException">Thrown when attempting to seek outside the bounds of the buffer.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the <paramref name="origin"/> is invalid.</exception>
    public override long Seek(long offset, SeekOrigin origin)
    {
        int newPosition = _position; // Default to current position
        switch (origin)
        {
            case SeekOrigin.Begin:
                newPosition = (int)offset;
                break;
            case SeekOrigin.Current:
                newPosition += (int)offset;
                break;
            case SeekOrigin.End:
                newPosition = _memory.Length + (int)offset;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(origin), "Invalid seek origin.");
        }

        if (newPosition < 0 || newPosition > _memory.Length)
            throw new IOException("Attempted to seek outside bounds of the buffer.");

        _position = newPosition;
        return _position;
    }

    /// <inheritdoc />
    public override void Flush() { }

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
#endif