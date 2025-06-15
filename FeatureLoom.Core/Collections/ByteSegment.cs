using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FeatureLoom.Extensions;
using FeatureLoom.Helpers;

namespace FeatureLoom.Collections;

/// <summary>
/// Represents a segment of a byte array, providing efficient, non-allocating operations and equality comparison.
/// </summary>
public struct ByteSegment : IEquatable<ByteSegment>, IEquatable<ArraySegment<byte>>, IEquatable<byte[]>, IReadOnlyList<byte>
{
    /// <summary>
    /// An empty <see cref="ByteSegment"/> instance.
    /// </summary>
    public static readonly ByteSegment Empty = new ByteSegment(Array.Empty<byte>());

    private readonly ArraySegment<byte> segment;
    private int? hashCode;        

    /// <summary>
    /// Initializes a new instance of the <see cref="ByteSegment"/> struct from an <see cref="ArraySegment{byte}"/>.
    /// </summary>
    public ByteSegment(ArraySegment<byte> segment)
    {
        this.segment = segment;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ByteSegment"/> struct from a byte array, offset, and count.
    /// </summary>
    public ByteSegment(byte[] array, int offset, int count)
    {
        segment = new ArraySegment<byte>(array, offset, count);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ByteSegment"/> struct from a byte array.
    /// </summary>
    public ByteSegment(byte[] array)
    {
        segment = new ArraySegment<byte>(array);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ByteSegment"/> struct from a string, using UTF-8 encoding.
    /// </summary>
    public ByteSegment(string str)
    {
        segment = new ArraySegment<byte>(str.ToByteArray());
    }

    /// <summary>
    /// Implicit conversion from <see cref="ArraySegment{byte}"/> to <see cref="ByteSegment"/>.
    /// </summary>
    public static implicit operator ByteSegment(ArraySegment<byte> segment)
    {
        return new ByteSegment(segment);
    }

    /// <summary>
    /// Implicit conversion from <see cref="byte[]"/> to <see cref="ByteSegment"/>.
    /// </summary>
    public static implicit operator ByteSegment(byte[] byteArray)
    {
        return new ByteSegment(new ArraySegment<byte>(byteArray));
    }

    /// <summary>
    /// Implicit conversion from <see cref="ByteSegment"/> to <see cref="ArraySegment{byte}"/>.
    /// </summary>
    public static implicit operator ArraySegment<byte>(ByteSegment wrapper)
    {
        return wrapper.segment;
    }

    /// <summary>
    /// Implicit conversion from <see cref="ByteSegment"/> to <see cref="byte[]"/>.
    /// Always returns a copy of the segment as a new array.
    /// </summary>
    public static implicit operator byte[](ByteSegment wrapper)
    {
        return wrapper.segment.ToArray();
    }

#if !NETSTANDARD2_0
    /// <summary>
    /// Returns the segment as a <see cref="Span{byte}"/> (only available on supported frameworks).
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
    /// Tries to find the index of the first occurrence of another <see cref="ByteSegment"/> within this segment.
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
    /// Gets the underlying <see cref="ArraySegment{byte}"/>.
    /// </summary>
    public ArraySegment<byte> AsArraySegment => segment;

    /// <summary>
    /// Returns a new array containing the bytes in the segment.
    /// </summary>
    public byte[] ToArray() => segment.ToArray();

    /// <summary>
    /// Enumerates the segment by splitting it at each occurrence of a separator byte.
    /// </summary>
    /// <param name="separator">The byte to split on.</param>
    /// <param name="skipEmpty">Whether to skip empty segments.</param>
    /// <returns>An enumerable of <see cref="ByteSegment"/>.</returns>
    public SplitEnumerator Split(byte separator, bool skipEmpty = false) => new SplitEnumerator(this, separator, skipEmpty);    

    /// <summary>
    /// Enumerator for splitting a <see cref="ByteSegment"/> by a separator byte.
    /// </summary>
    public struct SplitEnumerator : IEnumerator<ByteSegment>, IEnumerable<ByteSegment>
    {
        ByteSegment original;
        ByteSegment remaining;
        ByteSegment current;
        byte seperator;
        bool skipEmpty;
        bool finished;

        /// <summary>
        /// Initializes a new instance of the <see cref="SplitEnumerator"/> struct.
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
        /// Gets the current <see cref="ByteSegment"/>.
        /// </summary>
        public ByteSegment Current => current;

        object IEnumerator.Current => current;

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
        public IEnumerator<ByteSegment> GetEnumerator() => this;

        IEnumerator IEnumerable.GetEnumerator() => this;
    }

    /// <summary>
    /// Determines whether this segment is equal to another <see cref="ByteSegment"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ByteSegment other)
    {
        if (segment.Count != other.segment.Count) return false;            
        if (GetHashCode() != other.GetHashCode()) return false;            

        for (int i = 0; i < segment.Count; i++)
        {
            if (segment.Array[segment.Offset + i] != other.segment.Array[other.segment.Offset + i])
                return false;
        }

        return true;
    }

    /// <summary>
    /// Determines whether this segment is equal to an <see cref="ArraySegment{byte}"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ArraySegment<byte> other)
    {
        return Equals(new ByteSegment(other));
    }

    /// <summary>
    /// Determines whether this segment is equal to an <see cref="byte[]"/>.
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
    /// Determines whether two <see cref="ByteSegment"/> instances are equal.
    /// </summary>
    public static bool operator ==(ByteSegment left, ByteSegment right) => left.Equals(right);        

    /// <summary>
    /// Determines whether two <see cref="ByteSegment"/> instances are not equal.
    /// </summary>
    public static bool operator !=(ByteSegment left, ByteSegment right) => !left.Equals(right);

    /// <summary>
    /// Returns a hash code for this segment. The hash code is cached after the first calculation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        if (hashCode.HasValue) return hashCode.Value;
        hashCode = ComputeHashCode(AsArraySegment);
        return hashCode.Value;
    }

    /// <summary>
    /// Computes a hash code for the given <see cref="ArraySegment{byte}"/>.
    /// </summary>
    private static int ComputeHashCode(ArraySegment<byte> segment)
    {
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
    }

    /// <summary>
    /// Returns a string representation of the segment, decoding as UTF-8 if possible, otherwise as Base64.
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