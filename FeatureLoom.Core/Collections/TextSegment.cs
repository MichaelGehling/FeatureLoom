using FeatureLoom.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace FeatureLoom.Collections;

/// <summary>
/// Represents a segment of a string, providing efficient, non-allocating operations and value-based equality.
/// </summary>
public struct TextSegment : IReadOnlyList<char>, IEquatable<TextSegment>, IEquatable<string>
{
    /// <summary>
    /// An empty <see cref="TextSegment"/> instance.
    /// </summary>
    public static readonly TextSegment Empty = new TextSegment("");

    readonly string text;
    readonly int startIndex;
    readonly int length;
    int? hashCode;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextSegment"/> struct from a string.
    /// </summary>
    /// <param name="text">The source string.</param>
    public TextSegment(string text) : this()
    {
        if (text == null) throw new ArgumentNullException(nameof(text));

        this.text = text;
        this.startIndex = 0;
        this.length = text.Length;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TextSegment"/> struct from a string and a start index.
    /// </summary>
    /// <param name="text">The source string.</param>
    /// <param name="startIndex">The starting index of the segment.</param>
    public TextSegment(string text, int startIndex) : this()
    {
        if (text == null) throw new ArgumentNullException(nameof(text));
        if (startIndex < 0 || startIndex > text.Length) throw new ArgumentOutOfRangeException(nameof(startIndex));
            
        this.text = text;
        this.startIndex = startIndex;
        this.length = text.Length - startIndex;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TextSegment"/> struct from a string, start index, and length.
    /// </summary>
    /// <param name="text">The source string.</param>
    /// <param name="startIndex">The starting index of the segment.</param>
    /// <param name="length">The length of the segment.</param>
    public TextSegment(string text, int startIndex, int length) : this()
    {
        if (text == null) throw new ArgumentNullException(nameof(text));
        if (startIndex < 0 || length < 0 || startIndex + length > text.Length) throw new ArgumentOutOfRangeException();
            
        this.text = text;
        this.startIndex = startIndex;
        this.length = length;
    }

    /// <summary>
    /// Gets the number of characters in the segment.
    /// </summary>
    public int Count => length;

    /// <summary>
    /// Gets a value indicating whether the segment is valid (i.e., the underlying string is not null).
    /// </summary>
    public bool IsValid => text != null;

    /// <summary>
    /// Gets a value indicating whether the segment is empty or invalid.
    /// </summary>
    public bool IsEmptyOrInvalid => !IsValid || length == 0;

    /// <summary>
    /// Gets the start index of the segment within the original string.
    /// </summary>
    public int StartIndex => startIndex;

    /// <summary>
    /// Returns the string represented by this segment.
    /// </summary>
    /// <returns>The substring for this segment, or an empty string if the segment is empty.</returns>
    public override string ToString() => length == 0 ? "" : text.Substring(startIndex, length);

#if !NETSTANDARD2_0
    /// <summary>
    /// Returns the segment as a <see cref="ReadOnlySpan{char}"/> (only available on supported frameworks).
    /// </summary>
    public ReadOnlySpan<char> AsSpan() => text.AsSpan(startIndex, length);       
#endif

    /// <summary>
    /// Gets the character at the specified index within the segment.
    /// </summary>
    /// <param name="index">The zero-based index within the segment.</param>
    /// <returns>The character at the specified index.</returns>
    public char this[int index] => text[startIndex + index];

    /// <summary>
    /// Returns a subsegment starting at the specified index to the end of the segment.
    /// </summary>
    /// <param name="startIndex">The starting index of the subsegment, relative to this segment.</param>
    /// <returns>A new <see cref="TextSegment"/> representing the subsegment.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TextSegment SubSegment(int startIndex) => new TextSegment(text, this.startIndex + startIndex);

    /// <summary>
    /// Returns a subsegment starting at the specified index with the specified length.
    /// </summary>
    /// <param name="startIndex">The starting index of the subsegment, relative to this segment.</param>
    /// <param name="length">The length of the subsegment.</param>
    /// <returns>A new <see cref="TextSegment"/> representing the subsegment.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TextSegment SubSegment(int startIndex, int length) => new TextSegment(text, this.startIndex + startIndex, length);

    /// <summary>
    /// Tries to find the index of the first occurrence of another <see cref="TextSegment"/> within this segment.
    /// </summary>
    /// <param name="other">The segment to search for.</param>
    /// <param name="index">The index of the first occurrence, if found.</param>
    /// <returns>True if found; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFindIndex(TextSegment other, out int index)
    {
        for (index = 0; index < length; index++)
        {
            if (index + other.length > length) return false;
            bool found = true;
            for (int j = 0; j < other.length; j++)
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
    /// Tries to find the index of the first occurrence of a character within this segment.
    /// </summary>
    /// <param name="c">The character to search for.</param>
    /// <param name="index">The index of the first occurrence, if found.</param>
    /// <returns>True if found; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFindIndex(char c, out int index)
    {
        for (index = 0; index < length; index++)
        {
            if (text[startIndex + index] == c) return true;
        }
        return false;
    }

    /// <summary>
    /// Enumerator for splitting a <see cref="TextSegment"/> by a separator character.
    /// </summary>
    public struct SplitEnumerator : IEnumerator<TextSegment>
    {
        TextSegment original;
        TextSegment remaining;
        TextSegment current;
        char seperator;
        bool skipEmpty;
        bool finished;

        /// <summary>
        /// Initializes a new instance of the <see cref="SplitEnumerator"/> struct.
        /// </summary>
        /// <param name="original">The original segment to split.</param>
        /// <param name="seperator">The separator character.</param>
        /// <param name="skipEmpty">Whether to skip empty segments.</param>
        public SplitEnumerator(TextSegment original, char seperator, bool skipEmpty)
        {
            this.original = original;
            this.remaining = original;
            this.current = TextSegment.Empty;
            this.seperator = seperator;
            this.skipEmpty = skipEmpty;
            this.finished = false;
        }

        /// <summary>
        /// Gets the current <see cref="TextSegment"/>.
        /// </summary>
        public TextSegment Current => current;

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
                    if (current.length == 0 && skipEmpty) continue;
                    return true;
                }
                else
                {
                    current = remaining;
                    remaining = Empty;
                    if (current.length == 0 && skipEmpty) return false;
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
    }

    /// <summary>
    /// Enumerates the segment by splitting it at each occurrence of a separator character.
    /// </summary>
    /// <param name="separator">The character to split on.</param>
    /// <param name="skipEmpty">Whether to skip empty segments.</param>
    /// <returns>An enumerable of <see cref="TextSegment"/>.</returns>
    public EnumerableHelper<TextSegment, SplitEnumerator> Split(char separator, bool skipEmpty = false)
    {
        return new EnumerableHelper<TextSegment, SplitEnumerator>(new SplitEnumerator(this, separator, skipEmpty));
    }

    /// <summary>
    /// Returns an enumerator that iterates through the segment.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IEnumerator<char> GetEnumerator()
    {
        for (int i = 0; i < length; i++)
        {
            yield return text[startIndex + i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Implicit conversion from <see cref="string"/> to <see cref="TextSegment"/>.
    /// </summary>
    public static implicit operator TextSegment(string text) => new TextSegment(text);

    /// <summary>
    /// Implicit conversion from <see cref="TextSegment"/> to <see cref="string"/>.
    /// </summary>
    public static implicit operator string(TextSegment textSegment) => textSegment.ToString();

    /// <summary>
    /// Determines whether two <see cref="TextSegment"/> instances are equal.
    /// </summary>
    public static bool operator ==(TextSegment left, TextSegment right)
    {
        if (left.length != right.length) return false;
        if (left.GetHashCode() != right.GetHashCode()) return false;

        for (int i = 0; i < left.length; i++)
        {
            if (left[i] != right[i]) return false;
        }
        return true;
    }

    /// <summary>
    /// Determines whether two <see cref="TextSegment"/> instances are not equal.
    /// </summary>
    public static bool operator !=(TextSegment left, TextSegment right) => !(left == right);

    /// <summary>
    /// Determines whether this segment is equal to another <see cref="TextSegment"/>.
    /// </summary>
    public bool Equals(TextSegment other) => this == other;

    /// <summary>
    /// Determines whether this segment is equal to a <see cref="string"/>.
    /// </summary>
    public bool Equals(string other) => this == new TextSegment(other);

    /// <summary>
    /// Determines whether this segment is equal to another object.
    /// </summary>
    public override bool Equals(object obj)
    {
        if (obj is TextSegment textSegment) return this == textSegment;
        if (obj is string str) return this == new TextSegment(str);
        return false;
    }

    /// <summary>
    /// Returns a hash code for this segment. The hash code is cached after the first calculation.
    /// </summary>
    public override int GetHashCode()
    {
        if (!hashCode.HasValue) hashCode = ComputeHashCode();
        return hashCode.Value;
    }

    /// <summary>
    /// Computes a hash code for the segment.
    /// </summary>
    private int ComputeHashCode()
    {
        // Initial hash values.
        int hash1 = 5381;
        int hash2 = 5381;

        for (int i = 0; i < length; i++)
        {
            // Processing odd indexed characters with hash1 and even indexed characters with hash2.
            if (i % 2 == 0)
            {
                hash1 = ((hash1 << 5) + hash1) ^ this[i];
            }
            else
            {
                hash2 = ((hash2 << 5) + hash2) ^ this[i];
            }
        }

        // Combining the hash values.
        return hash1 + (hash2 * 1566083941);
    }
}
