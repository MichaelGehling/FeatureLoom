using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FeatureLoom.Extensions;
using FeatureLoom.Helpers;

namespace FeatureLoom.Collections;

/// <summary>
/// Represents a non-allocating value-type view over a segment of a byte array.
/// - Equality is optimized: first by length, then (when available) by cached hash code for an O(1) early-out,
///   and finally by a fast byte comparison (Span.SequenceEqual on modern targets or a tight loop on .NET Standard 2.0).
/// - Hash code generation is implicit and cached on first use (lazy); use <see cref="EnsureHashCode"/> to precompute
///   when the segment will be used as a key.
/// </summary>
public struct ByteSegment : IEquatable<ByteSegment>, IEquatable<System.ArraySegment<byte>>, IEquatable<byte[]>, IReadOnlyList<byte>
{
    /// <summary>
    /// An empty <see cref="FeatureLoom.Collections.ByteSegment"/> instance.
    /// </summary>
    public static readonly ByteSegment Empty = new ByteSegment(Array.Empty<byte>());

    private readonly ArraySegment<byte> segment;
    private int? hashCode;

    /// <summary>
    /// Initializes a new instance from an <see cref="System.ArraySegment{T}"/>.
    /// </summary>
    /// <param name="segment">The underlying byte segment.</param>
    /// <param name="initHash">If true, precompute and cache the hash code eagerly (useful for dictionary keys).</param>
    public ByteSegment(ArraySegment<byte> segment, bool initHash = false)
    {
        this.segment = segment;
        if (initHash) EnsureHashCode();
    }

    /// <summary>
    /// Initializes a new instance from a byte array, offset, and count.
    /// </summary>
    /// <param name="array">Source array.</param>
    /// <param name="offset">Start offset in the array.</param>
    /// <param name="count">Number of bytes.</param>
    /// <param name="initHash">If true, precompute and cache the hash code eagerly (useful for dictionary keys).</param>
    public ByteSegment(byte[] array, int offset, int count, bool initHash = false) 
    {
        segment = new ArraySegment<byte>(array, offset, count);
        if (initHash) EnsureHashCode();
    }

    /// <summary>
    /// Initializes a new instance from a byte array.
    /// </summary>
    /// <param name="array">Source array.</param>
    /// <param name="initHash">If true, precompute and cache the hash code eagerly (useful for dictionary keys).</param>
    public ByteSegment(byte[] array, bool initHash = false)
    {
        segment = new ArraySegment<byte>(array);
        if (initHash) EnsureHashCode();
    }

    /// <summary>
    /// Initializes a new instance from a string, using UTF-8 encoding.
    /// </summary>
    /// <param name="str">The source string.</param>
    /// <param name="initHash">If true, precompute and cache the hash code eagerly (useful for dictionary keys).</param>
    public ByteSegment(string str, bool initHash = false)
    {
        segment = new ArraySegment<byte>(str.ToByteArray());
        if (initHash) EnsureHashCode();
    }

    /// <summary>
    /// Implicit conversion from <see cref="System.ArraySegment{T}"/> to <see cref="FeatureLoom.Collections.ByteSegment"/>.
    /// </summary>
    public static implicit operator ByteSegment(ArraySegment<byte> segment)
    {
        return new ByteSegment(segment);
    }

    /// <summary>
    /// Implicit conversion from <see cref="byte"/>[] to <see cref="FeatureLoom.Collections.ByteSegment"/>.
    /// </summary>
    public static implicit operator ByteSegment(byte[] byteArray)
    {
        return new ByteSegment(new ArraySegment<byte>(byteArray));
    }

    /// <summary>
    /// Implicit conversion from <see cref="FeatureLoom.Collections.ByteSegment"/> to <see cref="System.ArraySegment{T}"/>.
    /// </summary>
    public static implicit operator ArraySegment<byte>(ByteSegment wrapper)
    {
        return wrapper.segment;
    }

    /// <summary>
    /// Implicit conversion from <see cref="FeatureLoom.Collections.ByteSegment"/> to <see cref="byte"/>[].
    /// Always returns a copy of the segment as a new array.
    /// </summary>
    public static implicit operator byte[](ByteSegment wrapper)
    {
        return wrapper.segment.ToArray();
    }

#if !NETSTANDARD2_0
    /// <summary>
    /// Returns the segment as a <see cref="System.Span{T}"/> (only available on supported frameworks).
    /// </summary>
    public Span<byte> AsSpan() => segment.AsSpan();        
#endif

    /// <summary>
    /// Gets the byte at the specified index within the segment.
    /// </summary>
    public byte this[int index] => segment.Get(index);

    /// <summary>
    /// Returns a subsegment starting at the specified index to the end of the segment.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ByteSegment SubSegment(int startIndex) => new ByteSegment(segment.Array, startIndex + segment.Offset, segment.Count - startIndex);

    /// <summary>
    /// Returns a subsegment starting at the specified index with the specified length.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ByteSegment SubSegment(int startIndex, int length) => new ByteSegment(segment.Array, startIndex + segment.Offset, length);

    /// <summary>
    /// Tries to find the index of the first occurrence of another <see cref="FeatureLoom.Collections.ByteSegment"/> within this segment.
    /// </summary>
    /// <param name="other">The segment to search for.</param>
    /// <param name="index">The index of the first occurrence, if found.</param>
    /// <returns>True if found; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFindIndex(ByteSegment other, out int index)
    {
        for (index = 0; index < segment.Count; index++)
        {
            if (index + other.segment.Count > segment.Count) return false;
            bool found = true;
            for (int j = 0; j < other.segment.Count; j++)
            {
                if (this[index + j] != other[j])
                {
                    found = false;
                    break;
                }
            }
            if (found) return true;
        }
        return false;
    }

    /// <summary>
    /// Tries to find the index of the first occurrence of a byte value within this segment.
    /// </summary>
    /// <param name="b">The byte to search for.</param>
    /// <param name="index">The index of the first occurrence, if found.</param>
    /// <returns>True if found; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFindIndex(byte b, out int index)
    {
        for (index = 0; index < segment.Count; index++)
        {
            if (this[index] == b) return true;
        }
        return false;
    }

    /// <summary>
    /// Gets a value indicating whether the segment is valid (i.e., the underlying array is not null).
    /// </summary>
    public bool IsValid => segment.Array != null;

    /// <summary>
    /// Gets the number of bytes in the segment.
    /// </summary>
    public int Count => segment.Count;

    /// <summary>
    /// Gets a value indicating whether the segment is empty or invalid.
    /// </summary>
    public bool IsEmptyOrInvalid => !IsValid || segment.Count == 0;

    /// <summary>
    /// Gets the underlying <see cref="System.ArraySegment{T}"/>.
    /// </summary>
    public ArraySegment<byte> AsArraySegment => segment;

    /// <summary>
    /// Returns a new array containing the bytes in the segment.
    /// </summary>
    public byte[] ToArray() => segment.ToArray();

    /// <summary>
    /// Enumerates the segment by splitting it at each occurrence of a separator byte.
    /// Allocation-free, forward-only enumerator intended for single-use iteration.
    /// </summary>
    /// <param name="separator">The byte to split on.</param>
    /// <param name="skipEmpty">Whether to skip empty segments.</param>
    /// <returns>An enumerable of <see cref="FeatureLoom.Collections.ByteSegment"/>.</returns>
    public SplitEnumerator Split(byte separator, bool skipEmpty = false) => new SplitEnumerator(this, separator, skipEmpty);    

    /// <summary>
    /// Enumerator for splitting a <see cref="FeatureLoom.Collections.ByteSegment"/> by a separator byte.
    /// Allocation-free and forward-only.
    /// </summary>
    public struct SplitEnumerator : IEnumerator<FeatureLoom.Collections.ByteSegment>, IEnumerable<FeatureLoom.Collections.ByteSegment>
    {
        ByteSegment original;
        ByteSegment remaining;
        ByteSegment current;
        byte seperator;
        bool skipEmpty;
        bool finished;

        /// <summary>
        /// Initializes a new instance of the <see cref="FeatureLoom.Collections.ByteSegment.SplitEnumerator"/> struct.
        /// </summary>
        public SplitEnumerator(ByteSegment original, byte seperator, bool skipEmpty)
        {
            this.original = original;
            remaining = original;
            current = Empty;
            this.seperator = seperator;
            this.skipEmpty = skipEmpty;
            finished = false;
        }

        /// <summary>
        /// Gets the current <see cref="FeatureLoom.Collections.ByteSegment"/>.
        /// </summary>
        public ByteSegment Current => current;

        object IEnumerator.Current => current;


        /// <summary>
        /// No-op. This enumerator does not hold unmanaged resources.
        /// </summary>
        public void Dispose() { }

        /// <summary>
        /// Advances the enumerator to the next segment.
        /// </summary>
        /// <returns>True if a segment is found; otherwise, false.</returns>
        public bool MoveNext()
        {
            if (finished) return false;

            while (true)
            {
                if (remaining.TryFindIndex(seperator, out int index))
                {
                    current = remaining.SubSegment(0, index);
                    remaining = remaining.SubSegment(index + 1);
                    if (current.Count == 0 && skipEmpty) continue;
                    return true;
                }
                else
                {
                    current = remaining;
                    remaining = Empty;
                    if (current.Count == 0 && skipEmpty) return false;
                    finished = true;
                    return true;
                }
            }
        }

        /// <summary>
        /// Resets the enumerator to its initial state.
        /// </summary>
        public void Reset()
        {
            remaining = original;
            finished = false;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the segments.
        /// </summary>
        public IEnumerator<FeatureLoom.Collections.ByteSegment> GetEnumerator() => this;

        IEnumerator IEnumerable.GetEnumerator() => this;
    }

    /// <summary>
    /// Determines whether this segment is equal to another <see cref="FeatureLoom.Collections.ByteSegment"/>.
    /// Fast path: returns false immediately when both sides have cached hash codes that differ.
    /// Otherwise, compares lengths and then performs a bytewise comparison (using <c>Span.SequenceEqual</c> when available).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ByteSegment other)
    {
        if (segment.Count != other.segment.Count) return false;            
        if (HasHashCode() && other.HasHashCode() && GetHashCode() != other.GetHashCode()) return false;
#if NETSTANDARD2_0
        for (int i = 0; i < segment.Count; i++)
        {
            if (segment.Array[segment.Offset + i] != other.segment.Array[other.segment.Offset + i])
                return false;
        }
        return false;
#else
        var span1 = segment.AsSpan();
        var span2 = other.segment.AsSpan();
        return span1.SequenceEqual(span2);        
#endif
    }

    /// <summary>
    /// Determines whether this segment is equal to an <see cref="System.ArraySegment{T}"/>.
    /// No allocations; wraps the other segment and uses the same fast comparison path.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ArraySegment<byte> other)
    {
        return Equals(new ByteSegment(other));
    }

    /// <summary>
    /// Determines whether this segment is equal to a <see cref="byte"/>[].
    /// No allocations; wraps the array and uses the same fast comparison path.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(byte[] other)
    {
        return Equals(new ByteSegment(other));
    }

    /// <summary>
    /// Determines whether this segment is equal to another object.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object obj)
    {
        return obj is ByteSegment other && Equals(other);
    }

    /// <summary>
    /// Determines whether two <see cref="FeatureLoom.Collections.ByteSegment"/> instances are equal.
    /// </summary>
    public static bool operator ==(ByteSegment left, ByteSegment right) => left.Equals(right);        

    /// <summary>
    /// Determines whether two <see cref="FeatureLoom.Collections.ByteSegment"/> instances are not equal.
    /// </summary>
    public static bool operator !=(ByteSegment left, ByteSegment right) => !left.Equals(right);

    /// <summary>
    /// Returns a hash code for this segment.
    /// The hash is computed once on first use (implicit, lazy) and cached thereafter. Call <see cref="EnsureHashCode"/>
    /// to precompute eagerly when the instance will be used as a dictionary key or stored in a set.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        if (hashCode.HasValue) return hashCode.Value;
        hashCode = ComputeHashCode(AsArraySegment);
        return hashCode.Value;
    }

    /// <summary>
    /// Ensures the hash code is computed and cached without changing semantics.
    /// Idempotent; safe to call multiple times.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureHashCode()
    {
        if (!hashCode.HasValue) hashCode = ComputeHashCode(AsArraySegment);
    }

    /// <summary>
    /// Indicates whether the hash code has already been computed and cached.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasHashCode()
    {
        return hashCode.HasValue;
    }

    /// <summary>
    /// Computes a non-cryptographic, order-sensitive hash code for the given segment.
    /// Linear time, unchecked arithmetic, using a common 17/23 mixing pattern.
    /// Uses <see cref="Unsafe.Add{T}(ref T, int)"/> to iterate efficiently without bounds checks.
    /// </summary>
    private static int ComputeHashCode(ArraySegment<byte> segment)
    {
#if NET8_0_OR_GREATER
        var h = new HashCode();
        h.AddBytes(segment.AsSpan());
        return h.ToHashCode();
#else
        unchecked // Overflow is fine, just wrap
        {                
            int hash = 17;
            if (segment.Count == 0) return hash;

            ref byte arrayRef = ref segment.Array[0];
            var limit = segment.Offset + segment.Count;
            for (int i = segment.Offset; i < limit; i++)
            {
                hash = hash * 23 + Unsafe.Add(ref arrayRef, i);
            }
            return hash;
        }
#endif
    }

    /// <summary>
    /// Returns a string representation of the segment.
    /// Attempts UTF-8 decoding; if that fails, falls back to Base64. Returns null if the segment is invalid.
    /// </summary>
    public override string ToString()
    {
        if (segment.Array == null) return null;
        try
        {
            return System.Text.Encoding.UTF8.GetString(segment.Array, segment.Offset, segment.Count);
        }
        catch
        {
            return Convert.ToBase64String(segment.Array, segment.Offset, segment.Count);
        }
    }

    /// <summary>
    /// Returns an enumerator that iterates through the segment.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IEnumerator<byte> GetEnumerator()
    {
        return ((IEnumerable<byte>)segment).GetEnumerator();
    }

    /// <summary>
    /// Returns an enumerator that iterates through the segment.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)segment).GetEnumerator();
    }
}