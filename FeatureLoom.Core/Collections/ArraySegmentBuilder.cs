using FeatureLoom.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FeatureLoom.Collections;

/// <summary>
/// Builds a combined <see cref="ArraySegment{T}"/> by appending multiple segments.
/// This avoids repeated full-array concatenation and helps reduce allocations for large or frequent appends.
/// The builder uses a <see cref="SlicedBuffer{T}"/> as backing storage.
/// The buffer can be provided externally, or the thread-local shared instance is used.
/// If no interfering buffer operations occur between appends and enough capacity is available,
/// the combined segment can stay in-place without additional array allocation.
/// </summary>
public sealed class ArraySegmentBuilder<T> : IDisposable, IReadOnlyList<T>
{
    static readonly bool isByteType = typeof(T) == typeof(byte);
    SlicedBuffer<T> buffer;
    readonly static ArraySegment<T> EmptySegment = new ArraySegment<T>(Array.Empty<T>());
    ArraySegment<T> combined = EmptySegment;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArraySegmentBuilder{T}"/> class.
    /// </summary>
    /// <param name="buffer">
    /// Optional backing <see cref="SlicedBuffer{T}"/>. If <see langword="null"/>, the thread's shared buffer is used.
    /// </param>
    public ArraySegmentBuilder(SlicedBuffer<T> buffer = null)
    {
        this.buffer = buffer;
    }

    /// <summary>
    /// Appends the specified segment and returns the resulting combined segment.
    /// The current combined slice is extended in the backing <see cref="SlicedBuffer{T}"/>, then the new segment content is copied.
    /// If the combined slice is still the most recent allocation in the buffer and capacity is sufficient,
    /// extension can happen in-place.
    /// </summary>
    /// <param name="segment">The segment to append.</param>
    /// <returns>The combined segment after the append operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ArraySegment<T> Append(ArraySegment<T> segment)
    {
        var buffer = this.buffer ?? SlicedBuffer<T>.Shared;
        int countBefore = combined.Count;
        buffer.ExtendSlice(ref combined, segment.Count);        

        if (segment.Count > 0)
        {
            if (isByteType)
            {
                Buffer.BlockCopy(
                    (byte[])(object)segment.Array,
                    segment.Offset,
                    (byte[])(object)combined.Array,
                    combined.Offset + countBefore,
                    segment.Count);
            }
            else
            {
                Array.Copy(
                    segment.Array,
                    segment.Offset,
                    combined.Array,
                    combined.Offset + countBefore,
                    segment.Count);
            }
        }

        return combined;
    }

    /// <summary>
    /// Gets the combined segment built so far.
    /// </summary>
    public ArraySegment<T> CombinedSegment => combined;

    /// <summary>
    /// Gets the number of elements currently contained in the combined segment.
    /// </summary>
    public int Count => combined.Count;

    /// <summary>
    /// Gets the element at the specified index within the combined segment.
    /// </summary>
    /// <param name="index">Zero-based element index.</param>
    /// <returns>The element at the specified index.</returns>
    public T this[int index] => combined.Array[combined.Offset + index];

    /// <summary>
    /// Clears the builder state.
    /// </summary>
    /// <remarks>
    /// If <paramref name="unsafeReuse"/> is <see langword="true"/>, the current slice is returned to the backing
    /// <see cref="SlicedBuffer{T}"/> (when possible), allowing immediate reuse of that memory region.
    /// Previously returned segments may then observe modified data.
    /// If <paramref name="unsafeReuse"/> is <see langword="false"/>, only the builder state is reset.
    /// </remarks>
    /// <param name="unsafeReuse">
    /// <see langword="true"/> to return the current slice to the buffer for reuse; otherwise only resets builder state.
    /// </param>
    public void Clear(bool unsafeReuse = false)
    {
        if (unsafeReuse)
        {
            var buffer = this.buffer ?? SlicedBuffer<T>.Shared;
            buffer.FreeSlice(ref combined);
        }
        combined = EmptySegment;
    }

    /// <summary>
    /// Releases resources used by this instance.
    /// </summary>
    /// <remarks>
    /// After disposal, the instance should not be used.
    /// </remarks>
    public void Dispose()
    {
        Clear();
        buffer = null;
    }

    /// <summary>
    /// Returns a generic enumerator over the combined segment.
    /// </summary>
    /// <returns>An enumerator for iterating the current combined elements.</returns>
#if !NETSTANDARD2_0
    public System.ArraySegment<T>.Enumerator GetEnumerator()
    {
        return combined.GetEnumerator();
    }
    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return combined.GetEnumerator();
    }
#else
    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < combined.Count; i++)
        {
            yield return combined.Array[combined.Offset + i];
        }
    }
#endif

    /// <summary>
    /// Returns a non-generic enumerator over the combined segment.
    /// </summary>
    /// <returns>A non-generic enumerator for iterating the current combined elements.</returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable<T>)this).GetEnumerator();
    }
}
