using FeatureLoom.Extensions;
using FeatureLoom.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace FeatureLoom.Collections;

/// <summary>
/// Represents a segment of a string, providing efficient, non-allocating operations and value-based equality.
/// </summary>
public struct TextSegment : IReadOnlyList<char>, IEquatable<TextSegment>, IEquatable<string>, IConvertible
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
    /// Gets the number of characters in the segment (same as <see cref="Count"/>).
    /// Can be used for compatibility with APIs expecting a "Length" property.
    /// </summary>
    public int Length => length;

    /// <summary>
    /// Gets a value indicating whether the segment is valid (i.e., the underlying string is not null).
    /// </summary>
    public bool IsValid => text != null;

    /// <summary>
    /// Gets a value indicating whether the segment is empty or invalid.
    /// </summary>
    public bool IsEmptyOrInvalid => !IsValid || length == 0;

    /// <summary>
    /// Gets the start index of the segment within the underlying string.
    /// </summary>
    public int Offset => startIndex;

    /// <summary>
    /// Gets the underlying string value.
    /// </summary>
    public string UnderlyingString => text;

    /// <summary>
    /// Returns the string represented by this segment.
    /// </summary>
    /// <returns>The substring for this segment, or an empty string if the segment is empty.</returns>
    public override string ToString()
    {
        if (length == 0) return "";
        if (startIndex == 0 && length == text.Length) return text;
        else return text.Substring(startIndex, length);
    }

    /// <summary>
    /// Returns the string represented by this segment, deduplicated through a
    /// <see cref="StringInternCache"/>. When the same content has been produced before, the shared
    /// cached instance is returned instead of allocating a new substring, reducing heap usage and
    /// GC pressure when identical segments are converted repeatedly (e.g. recurring tokens while
    /// parsing).
    /// </summary>
    /// <param name="cache">
    /// The cache to use. If <c>null</c>, <see cref="StringInternCache.Shared"/> is used.
    /// </param>
    /// <returns>The deduplicated string value (value-equal to <see cref="TextSegment.ToString()"/>).</returns>
    /// <remarks>
    /// On frameworks that support spans, a cache hit avoids allocating a new substring entirely.
    /// Only value equality is guaranteed, not stable reference identity (see
    /// <see cref="StringInternCache"/>).
    /// </remarks>
    public string ToStringCached(StringInternCache cache = null)
    {
        if (length == 0) return "";

        cache = cache ?? StringInternCache.Shared;

        // If the segment covers the whole underlying string, deduplicate that instance directly:
        // no substring/span materialization is needed and the existing string can become the shared one.
        if (startIndex == 0 && length == text.Length) return cache.Intern(text);

#if !NETSTANDARD2_0
        return cache.Intern(AsSpan());
#else
        return cache.Intern(ToString());
#endif
    }

#if !NETSTANDARD2_0
    /// <summary>
    /// Returns the segment as a <see cref="ReadOnlySpan{T}"/> (only available on supported frameworks).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<char> AsSpan() => text.AsSpan(startIndex, length);       
#endif

    /// <summary>
    /// Gets the character at the specified index within the segment.
    /// </summary>
    /// <param name="index">The zero-based index within the segment.</param>
    /// <returns>The character at the specified index.</returns>
    /// <remarks>
    /// For performance reasons the index is not validated against the segment bounds. Only the bounds of the
    /// underlying string are checked, so an out-of-segment index may return a character outside of this segment.
    /// </remarks>
    public char this[int index] => text[startIndex + index];

    /// <summary>
    /// Returns a subsegment starting at the specified index to the end of the segment.
    /// </summary>
    /// <param name="startIndex">The starting index of the subsegment, relative to this segment.</param>
    /// <returns>A new <see cref="TextSegment"/> representing the subsegment.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="startIndex"/> is greater than <see cref="Length"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TextSegment SubSegment(int startIndex) => new TextSegment(text, this.startIndex + startIndex, length - startIndex);

    /// <summary>
    /// Returns a subsegment starting at the specified index with the specified length.
    /// </summary>
    /// <param name="startIndex">The starting index of the subsegment, relative to this segment.</param>
    /// <param name="length">The length of the subsegment.</param>
    /// <returns>A new <see cref="TextSegment"/> representing the subsegment.</returns>
    /// <remarks>
    /// Only the bounds of the underlying string are validated, not the bounds of this segment.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TextSegment SubSegment(int startIndex, int length) => new TextSegment(text, this.startIndex + startIndex, length);

    /// <summary>
    /// Returns a subsegment of this <see cref="TextSegment"/> that starts after the specified <paramref name="startAfter"/> segment
    /// and ends before the specified <paramref name="endBefore"/> segment.
    /// </summary>
    /// <param name="startIndex">The starting index to begin searching from, relative to this segment.</param>
    /// <param name="startAfter">The segment after which the subsegment should start. If empty or null, starts from <paramref name="startIndex"/>.</param>
    /// <param name="endBefore">The segment before which the subsegment should end. If empty or null, ends at the end of this segment.</param>
    /// <param name="restStartIndex">
    /// Outputs the index (relative to this segment) where processing can continue: the start of the found
    /// <paramref name="endBefore"/> occurrence, or the end of this segment if <paramref name="endBefore"/> is empty.
    /// If a marker is not found, it is the index where the failed search started.
    /// </param>
    /// <param name="includeSearchStrings">If true, includes the search strings in the result.</param>
    /// <returns>
    /// A <see cref="TextSegment"/> representing the subsegment, or null if one of the markers was not found.
    /// </returns>
    /// <remarks>
    /// <paramref name="endBefore"/> is always searched after the end of the found <paramref name="startAfter"/> occurrence,
    /// independent of <paramref name="includeSearchStrings"/>.
    /// </remarks>
    public TextSegment? SubSegment(int startIndex, TextSegment startAfter, TextSegment endBefore, out int restStartIndex, bool includeSearchStrings = false)
    {
        int startPos = startIndex;
        int searchEndFrom = startIndex;
        int endPos = Count;

        // Find startAfter
        if (!startAfter.IsEmptyOrInvalid)
        {
            if (!TryFindIndex(startAfter, startIndex, out int foundStart))
            {
                restStartIndex = startIndex;
                return null;
            }
            searchEndFrom = foundStart + startAfter.Count;
            startPos = includeSearchStrings ? foundStart : searchEndFrom;
        }

        // Find endBefore (never inside the startAfter marker, even if it is included in the result)
        int foundEnd = -1;
        if (!endBefore.IsEmptyOrInvalid)
        {
            if (!TryFindIndex(endBefore, searchEndFrom, out foundEnd))
            {
                restStartIndex = searchEndFrom;
                return null;
            }
            endPos = foundEnd;
            if (includeSearchStrings) endPos += endBefore.Count;
            restStartIndex = foundEnd;
        }
        else
        {
            restStartIndex = endPos;
        }

        int subLength = endPos - startPos;
        if (subLength < 0) return null;
        return SubSegment(startPos, subLength);
    }

    /// <summary>
    /// Returns a string representing the subsegment that starts after the specified <paramref name="startAfter"/> segment and ends before the specified <paramref name="endBefore"/> segment.
    /// </summary>
    /// <param name="startAfter">The segment after which the subsegment should start.</param>
    /// <param name="endBefore">The segment before which the subsegment should end.</param>
    /// <param name="includeSearchStrings">If true, includes the search strings in the result.</param>
    /// <returns>The substring for the specified subsegment, or null if not found.</returns>
    public string SubSegment(TextSegment startAfter, TextSegment endBefore, bool includeSearchStrings = false)
    {
        return SubSegment(0, startAfter, endBefore, out _, includeSearchStrings);
    }

    /// <summary>
    /// Returns a string representing the subsegment that starts after the specified <paramref name="startAfter"/> segment and ends at the end of this segment.
    /// </summary>
    /// <param name="startAfter">The segment after which the subsegment should start.</param>
    /// <param name="includeSearchStrings">If true, includes the search string in the result.</param>
    /// <returns>The substring for the specified subsegment, or null if not found.</returns>
    public string SubSegment(TextSegment startAfter, bool includeSearchStrings = false)
    {
        return SubSegment(0, startAfter, Empty, out _, includeSearchStrings);
    }

    /// <summary>
    /// Determines whether this segment starts with the specified character.
    /// </summary>
    /// <param name="c">The character to check for at the start of the segment.</param>
    /// <returns>True if the segment starts with the specified character; otherwise, false.</returns>
    public bool StartsWith(char c)
    {
        return !IsEmptyOrInvalid && this[0] == c;
    }

    /// <summary>
    /// Determines whether this segment ends with the specified character.
    /// </summary>
    /// <param name="c">The character to check for at the end of the segment.</param>
    /// <returns>True if the segment ends with the specified character; otherwise, false.</returns>
    public bool EndsWith(char c)
    {
        return !IsEmptyOrInvalid && this[length - 1] == c;
    }

    /// <summary>
    /// Determines whether this segment contains the specified character.
    /// </summary>
    /// <param name="c">The character to search for.</param>
    /// <returns>True if the segment contains the specified character; otherwise, false.</returns>
    public bool Contains(char c)
    {
        if (length == 0) return false;
        return text.IndexOf(c, startIndex, length) >= 0;
    }

    /// <summary>
    /// Tries to find the index of the first occurrence of another <see cref="TextSegment"/> within this segment.
    /// </summary>
    /// <param name="other">The segment to search for.</param>
    /// <param name="index">The index of the first occurrence, if found.</param>
    /// <returns>True if found; otherwise, false.</returns>
    /// <remarks>
    /// Same semantics as <see cref="string.IndexOf(string, StringComparison)"/> with ordinal comparison:
    /// an empty <paramref name="other"/> is always found at index 0 (also within an empty segment).
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFindIndex(TextSegment other, out int index) => TryFindIndex(other, 0, out index);

    /// <summary>
    /// Tries to find the index of the first occurrence of another <see cref="TextSegment"/> within this segment,
    /// starting the search at <paramref name="firstIndex"/>.
    /// </summary>
    /// <param name="other">The segment to search for.</param>
    /// <param name="firstIndex">The index (relative to this segment) where the search starts. Must be in the range [0, <see cref="Length"/>].</param>
    /// <param name="index">The index of the first occurrence, if found; otherwise -1.</param>
    /// <returns>True if found; otherwise, false.</returns>
    /// <remarks>
    /// Same semantics as <see cref="string.IndexOf(string, int, StringComparison)"/> with ordinal comparison:
    /// an empty <paramref name="other"/> is always found at <paramref name="firstIndex"/>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="firstIndex"/> is negative or greater than <see cref="Length"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFindIndex(TextSegment other, int firstIndex, out int index)
    {
        if ((uint)firstIndex > (uint)length) throw new ArgumentOutOfRangeException(nameof(firstIndex));

        if (other.length == 0)
        {
            index = firstIndex;
            return true;
        }

#if !NETSTANDARD2_0
        index = AsSpan().Slice(firstIndex).IndexOf(other.AsSpan());
        if (index < 0) return false;
        index += firstIndex;
        return true;
#else
        // Scan for the first char natively, then verify the rest with an ordinal compare.
        int otherLength = other.length;
        int lastStart = startIndex + length - otherLength;
        char first = other.text[other.startIndex];
        int pos = startIndex + firstIndex;
        while (pos <= lastStart)
        {
            pos = text.IndexOf(first, pos, lastStart - pos + 1);
            if (pos < 0) break;
            if (string.CompareOrdinal(text, pos + 1, other.text, other.startIndex + 1, otherLength - 1) == 0)
            {
                index = pos - startIndex;
                return true;
            }
            pos++;
        }
        index = -1;
        return false;
#endif
    }

    /// <summary>
    /// Tries to find the index of the first occurrence of a character within this segment.
    /// </summary>
    /// <param name="c">The character to search for.</param>
    /// <param name="index">The index of the first occurrence, if found; otherwise -1.</param>
    /// <returns>True if found; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFindIndex(char c, out int index)
    {
        if (length == 0)
        {
            index = -1;
            return false;
        }
        index = text.IndexOf(c, startIndex, length);
        if (index < 0) return false;
        index -= startIndex;
        return true;
    }

    /// <summary>
    /// Tries to find the index of the first occurrence of a character within this segment,
    /// starting the search at <paramref name="firstIndex"/>.
    /// </summary>
    /// <param name="c">The character to search for.</param>
    /// <param name="firstIndex">The index (relative to this segment) where the search starts. Must be in the range [0, <see cref="Length"/>].</param>
    /// <param name="index">The index of the first occurrence, if found; otherwise -1.</param>
    /// <returns>True if found; otherwise, false.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="firstIndex"/> is negative or greater than <see cref="Length"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFindIndex(char c, int firstIndex, out int index)
    {
        if ((uint)firstIndex > (uint)length) throw new ArgumentOutOfRangeException(nameof(firstIndex));

        int count = length - firstIndex;
        if (count == 0)
        {
            index = -1;
            return false;
        }
        index = text.IndexOf(c, startIndex + firstIndex, count);
        if (index < 0) return false;
        index -= startIndex;
        return true;
    }

    /// <summary>
    /// Tries to find the index of the last occurrence of another <see cref="TextSegment"/> within this segment.
    /// </summary>
    /// <param name="other">The segment to search for.</param>
    /// <param name="index">The index of the last occurrence, if found; otherwise -1.</param>
    /// <returns>True if found; otherwise, false.</returns>
    /// <remarks>
    /// Same semantics as <see cref="string.LastIndexOf(string, StringComparison)"/> with ordinal comparison on .NET 5+:
    /// an empty <paramref name="other"/> is always found at <see cref="Length"/>.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFindLastIndex(TextSegment other, out int index) => TryFindLastIndex(other, length - 1, out index);

    /// <summary>
    /// Tries to find the index of the last occurrence of another <see cref="TextSegment"/> within this segment,
    /// searching backward from <paramref name="lastIndex"/>. The entire <paramref name="other"/> segment
    /// must fit within the range [0, lastIndex].
    /// </summary>
    /// <param name="other">The segment to search for.</param>
    /// <param name="lastIndex">
    /// The index (relative to this segment) to start searching backward from. The last character of <paramref name="other"/>
    /// must be at or before this index. Must be in the range [-1, <see cref="Length"/> - 1]; -1 denotes an empty search range.
    /// </param>
    /// <param name="index">The index of the last occurrence, if found; otherwise -1.</param>
    /// <returns>True if found; otherwise, false.</returns>
    /// <remarks>
    /// An empty <paramref name="other"/> is always found at <paramref name="lastIndex"/> + 1 (the end of the search range).
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="lastIndex"/> is less than -1 or not less than <see cref="Length"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFindLastIndex(TextSegment other, int lastIndex, out int index)
    {
        if (lastIndex < -1 || lastIndex >= length) throw new ArgumentOutOfRangeException(nameof(lastIndex));

        if (other.length == 0)
        {
            index = lastIndex + 1;
            return true;
        }

        if (other.length > lastIndex + 1)
        {
            index = -1;
            return false;
        }
#if !NETSTANDARD2_0
        index = AsSpan().Slice(0, lastIndex + 1).LastIndexOf(other.AsSpan());
        return index >= 0;
#else
        // Scan backward for the first char natively, then verify the rest with an ordinal compare.
        int otherLength = other.length;
        char first = other.text[other.startIndex];
        int pos = startIndex + lastIndex - otherLength + 1;
        while (pos >= startIndex)
        {
            pos = text.LastIndexOf(first, pos, pos - startIndex + 1);
            if (pos < 0) break;
            if (string.CompareOrdinal(text, pos + 1, other.text, other.startIndex + 1, otherLength - 1) == 0)
            {
                index = pos - startIndex;
                return true;
            }
            pos--;
        }
        index = -1;
        return false;
#endif
    }

    /// <summary>
    /// Tries to find the index of the last occurrence of a character within this segment.
    /// </summary>
    /// <param name="c">The character to search for.</param>
    /// <param name="index">The index of the last occurrence, if found; otherwise -1.</param>
    /// <returns>True if found; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFindLastIndex(char c, out int index)
    {
        if (length == 0)
        {
            index = -1;
            return false;
        }
        index = text.LastIndexOf(c, startIndex + length - 1, length);
        if (index < 0) return false;
        index -= startIndex;
        return true;
    }

    /// <summary>
    /// Tries to find the index of the last occurrence of a character within this segment, searching backward from <paramref name="lastIndex"/>.
    /// </summary>
    /// <param name="c">The character to search for.</param>
    /// <param name="lastIndex">
    /// The index (relative to this segment) to start searching backward from.
    /// Must be in the range [-1, <see cref="Length"/> - 1]; -1 denotes an empty search range.
    /// </param>
    /// <param name="index">The index of the last occurrence, if found; otherwise -1.</param>
    /// <returns>True if found; otherwise, false.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="lastIndex"/> is less than -1 or not less than <see cref="Length"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFindLastIndex(char c, int lastIndex, out int index)
    {
        if (lastIndex < -1 || lastIndex >= length) throw new ArgumentOutOfRangeException(nameof(lastIndex));

        if (lastIndex < 0)
        {
            index = -1;
            return false;
        }
        index = text.LastIndexOf(c, startIndex + lastIndex, lastIndex + 1);
        if (index < 0) return false;
        index -= startIndex;
        return true;
    }

    /// <summary>
    /// Attempts to extract a value of type <typeparamref name="T"/> from a subsegment defined by the given boundaries.
    /// </summary>
    /// <typeparam name="T">The type to convert the extracted subsegment to.</typeparam>
    /// <param name="startIndex">The starting index to begin searching from, relative to this segment.</param>
    /// <param name="startExtractAfter">The segment after which extraction should start. If empty or null, starts from <paramref name="startIndex"/>.</param>
    /// <param name="endExtractBefore">The segment before which extraction should end. If empty or null, ends at the end of this segment.</param>
    /// <param name="extract">The extracted and converted value, if successful.</param>
    /// <param name="restStartIndex">Outputs the index after the end of the extracted subsegment, relative to this segment.</param>
    /// <param name="includeSearchStrings">If true, includes the search strings in the result.</param>
    /// <returns>
    /// <c>true</c> if extraction and conversion succeeded; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// Conversion uses <see cref="CultureInfo.InvariantCulture"/>. See
    /// <see cref="SubSegment(int, TextSegment, TextSegment, out int, bool)"/> for the boundary semantics.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryExtract<T>(int startIndex, TextSegment startExtractAfter, TextSegment endExtractBefore, out T extract, out int restStartIndex, bool includeSearchStrings = false) where T : IConvertible
    {
        extract = default;
        var subsegment = SubSegment(startIndex, startExtractAfter, endExtractBefore, out restStartIndex, includeSearchStrings);
        if (subsegment == null) return false;        
        return subsegment.Value.TryToType(out extract, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Attempts to extract a value of type <typeparamref name="T"/> located between <paramref name="startExtractAfter"/>
    /// and <paramref name="endExtractBefore"/>, searching from the start of this segment.
    /// </summary>
    /// <typeparam name="T">The type to convert the extracted subsegment to.</typeparam>
    /// <param name="startExtractAfter">The segment after which extraction should start. If empty, starts at the beginning.</param>
    /// <param name="endExtractBefore">The segment before which extraction should end. If empty, ends at the end of this segment.</param>
    /// <param name="extract">The extracted and converted value, if successful.</param>
    /// <returns><c>true</c> if extraction and conversion succeeded; otherwise, <c>false</c>.</returns>
    public bool TryExtract<T>(TextSegment startExtractAfter, TextSegment endExtractBefore, out T extract) where T : IConvertible
    {
        return TryExtract(0, startExtractAfter, endExtractBefore, out extract, out _);
    }

    /// <summary>
    /// Attempts to extract a value of type <typeparamref name="T"/> located between <paramref name="startExtractAfter"/>
    /// and <paramref name="endExtractBefore"/>, searching from <paramref name="startIndex"/>.
    /// </summary>
    /// <typeparam name="T">The type to convert the extracted subsegment to.</typeparam>
    /// <param name="startIndex">The starting index to begin searching from, relative to this segment.</param>
    /// <param name="startExtractAfter">The segment after which extraction should start. If empty, starts at <paramref name="startIndex"/>.</param>
    /// <param name="endExtractBefore">The segment before which extraction should end. If empty, ends at the end of this segment.</param>
    /// <param name="extract">The extracted and converted value, if successful.</param>
    /// <returns><c>true</c> if extraction and conversion succeeded; otherwise, <c>false</c>.</returns>
    public bool TryExtract<T>(int startIndex, TextSegment startExtractAfter, TextSegment endExtractBefore, out T extract) where T : IConvertible
    {
        return TryExtract(startIndex, startExtractAfter, endExtractBefore, out extract, out _);
    }

    /// <summary>
    /// Attempts to extract a value of type <typeparamref name="T"/> located between <paramref name="startExtractAfter"/>
    /// and <paramref name="endExtractBefore"/>, searching from the start of this segment.
    /// </summary>
    /// <typeparam name="T">The type to convert the extracted subsegment to.</typeparam>
    /// <param name="startExtractAfter">The segment after which extraction should start. If empty, starts at the beginning.</param>
    /// <param name="endExtractBefore">The segment before which extraction should end. If empty, ends at the end of this segment.</param>
    /// <param name="extract">The extracted and converted value, if successful.</param>
    /// <param name="restStartIndex">Outputs the index where processing can continue (see <see cref="SubSegment(int, TextSegment, TextSegment, out int, bool)"/>).</param>
    /// <returns><c>true</c> if extraction and conversion succeeded; otherwise, <c>false</c>.</returns>
    public bool TryExtract<T>(TextSegment startExtractAfter, TextSegment endExtractBefore, out T extract, out int restStartIndex) where T : IConvertible
    {
        return TryExtract(0, startExtractAfter, endExtractBefore, out extract, out restStartIndex);
    }

    /// <summary>
    /// Returns a new <see cref="TextSegment"/> with all leading or trailing whitespaces removed.
    /// </summary>
    public TextSegment Trim()
    {
        if (IsEmptyOrInvalid) return this;

        int newStart = startIndex;
        int newEnd = startIndex + length - 1;

        // Trim start
        while (newStart <= newEnd && char.IsWhiteSpace(text[newStart]))
        {
            newStart++;
        }

        // Trim end
        while (newEnd >= newStart && char.IsWhiteSpace(text[newEnd]))
        {
            newEnd--;
        }

        int newLength = newEnd - newStart + 1;
        if (newLength <= 0) return Empty;

        return new TextSegment(text, newStart, newLength);
    }

    /// <summary>
    /// Returns a new <see cref="TextSegment"/> with all leading or trailing characters contained in <paramref name="trimChars"/> removed.
    /// </summary>
    /// <param name="trimChars">The characters to remove. If null or empty, the segment is returned unchanged (unlike <see cref="string.Trim(char[])"/>, whitespace is not trimmed).</param>
    public TextSegment Trim(params char[] trimChars)
    {
        if (IsEmptyOrInvalid || trimChars == null || trimChars.Length == 0) return this;

        int newStart = startIndex;
        int newEnd = startIndex + length - 1;

        // Trim start
        while (newStart <= newEnd && IsTrimChar(trimChars, text[newStart]))
        {
            newStart++;
        }

        // Trim end
        while (newEnd >= newStart && IsTrimChar(trimChars, text[newEnd]))
        {
            newEnd--;
        }

        int newLength = newEnd - newStart + 1;
        if (newLength <= 0) return Empty;

        return new TextSegment(text, newStart, newLength);
    }

    /// <summary>
    /// Returns a new <see cref="TextSegment"/> with all leading or trailing characters equal to <paramref name="trimChar"/> removed.
    /// </summary>
    public TextSegment Trim(char trimChar)
    {
        if (IsEmptyOrInvalid) return this;

        int newStart = startIndex;
        int newEnd = startIndex + length - 1;

        // Trim start
        while (newStart <= newEnd && trimChar == text[newStart])
        {
            newStart++;
        }

        // Trim end
        while (newEnd >= newStart && trimChar == text[newEnd])
        {
            newEnd--;
        }

        int newLength = newEnd - newStart + 1;
        if (newLength <= 0) return Empty;

        return new TextSegment(text, newStart, newLength);
    }

    /// <summary>
    /// Returns a new <see cref="TextSegment"/> with all leading characters equal to <paramref name="trimChar"/> removed.
    /// </summary>
    public TextSegment TrimStart(char trimChar)
    {
        if (IsEmptyOrInvalid) return this;

        int newStart = startIndex;
        int newEnd = startIndex + length - 1;

        while (newStart <= newEnd && text[newStart] == trimChar)
        {
            newStart++;
        }

        int newLength = newEnd - newStart + 1;
        if (newLength <= 0) return Empty;

        return new TextSegment(text, newStart, newLength);
    }

    /// <summary>
    /// Returns a new <see cref="TextSegment"/> with all trailing characters equal to <paramref name="trimChar"/> removed.
    /// </summary>
    public TextSegment TrimEnd(char trimChar)
    {
        if (IsEmptyOrInvalid) return this;

        int newStart = startIndex;
        int newEnd = startIndex + length - 1;

        while (newEnd >= newStart && text[newEnd] == trimChar)
        {
            newEnd--;
        }

        int newLength = newEnd - newStart + 1;
        if (newLength <= 0) return Empty;

        return new TextSegment(text, newStart, newLength);
    }

    /// <summary>
    /// Returns a new <see cref="TextSegment"/> with all leading whitespaces removed.
    /// </summary>
    public TextSegment TrimStart()
    {
        if (IsEmptyOrInvalid) return this;

        int newStart = startIndex;
        int newEnd = startIndex + length - 1;

        while (newStart <= newEnd && char.IsWhiteSpace(text[newStart]))
        {
            newStart++;
        }

        int newLength = newEnd - newStart + 1;
        if (newLength <= 0) return Empty;

        return new TextSegment(text, newStart, newLength);
    }

    /// <summary>
    /// Returns a new <see cref="TextSegment"/> with all trailing whitespaces removed.
    /// </summary>
    public TextSegment TrimEnd()
    {
        if (IsEmptyOrInvalid) return this;

        int newStart = startIndex;
        int newEnd = startIndex + length - 1;

        while (newEnd >= newStart && char.IsWhiteSpace(text[newEnd]))
        {
            newEnd--;
        }

        int newLength = newEnd - newStart + 1;
        if (newLength <= 0) return Empty;

        return new TextSegment(text, newStart, newLength);
    }

    /// <summary>
    /// Returns a new <see cref="TextSegment"/> with all leading characters equal to <paramref name="trimChars"/> removed.
    /// </summary>
    public TextSegment TrimStart(params char[] trimChars)
    {
        if (IsEmptyOrInvalid || trimChars == null || trimChars.Length == 0) return this;

        int newStart = startIndex;
        int newEnd = startIndex + length - 1;

        // Trim start
        while (newStart <= newEnd && IsTrimChar(trimChars, text[newStart]))
        {
            newStart++;
        }

        int newLength = newEnd - newStart + 1;
        if (newLength <= 0) return Empty;

        return new TextSegment(text, newStart, newLength);
    }

    /// <summary>
    /// Returns a new <see cref="TextSegment"/> with all trailing characters equal to <paramref name="trimChars"/> removed.
    /// </summary>
    public TextSegment TrimEnd(params char[] trimChars)
    {
        if (IsEmptyOrInvalid || trimChars == null || trimChars.Length == 0) return this;

        int newStart = startIndex;
        int newEnd = startIndex + length - 1;

        // Trim end
        while (newEnd >= newStart && IsTrimChar(trimChars, text[newEnd]))
        {
            newEnd--;
        }

        int newLength = newEnd - newStart + 1;
        if (newLength <= 0) return Empty;

        return new TextSegment(text, newStart, newLength);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsTrimChar(char[] trimChars, char c)
    {
        for (int i = 0; i < trimChars.Length; i++)
        {
            if (trimChars[i] == c) return true;
        }
        return false;
    }

    /// <summary>
    /// Returns a new <see cref="TextSegment"/> with all leading occurrences of the specified string removed.
    /// </summary>
    /// <param name="trimStr">The string to remove from the start.</param>
    /// <returns>A new <see cref="TextSegment"/> with the specified string removed from the start.</returns>
    public TextSegment TrimStart(string trimStr)
    {
        if (IsEmptyOrInvalid || string.IsNullOrEmpty(trimStr)) return this;
        var trimSegment = new TextSegment(trimStr);

        int newStart = startIndex;
        int newEnd = startIndex + length - 1;

        while (newEnd - newStart + 1 >= trimSegment.length)
        {
            var candidate = new TextSegment(text, newStart, trimSegment.length);
            if (!candidate.Equals(trimSegment)) break;
            newStart += trimSegment.length;
        }

        int newLength = newEnd - newStart + 1;
        if (newLength <= 0) return Empty;
        return new TextSegment(text, newStart, newLength);
    }

    /// <summary>
    /// Returns a new <see cref="TextSegment"/> with all trailing occurrences of the specified string removed.
    /// </summary>
    /// <param name="trimStr">The string to remove from the end.</param>
    /// <returns>A new <see cref="TextSegment"/> with the specified string removed from the end.</returns>
    public TextSegment TrimEnd(string trimStr)
    {
        if (IsEmptyOrInvalid || string.IsNullOrEmpty(trimStr)) return this;
        var trimSegment = new TextSegment(trimStr);

        int newStart = startIndex;
        int newEnd = startIndex + length - 1;

        while (newEnd - newStart + 1 >= trimSegment.length)
        {
            var candidate = new TextSegment(text, newEnd - trimSegment.length + 1, trimSegment.length);
            if (!candidate.Equals(trimSegment)) break;
            newEnd -= trimSegment.length;
        }

        int newLength = newEnd - newStart + 1;
        if (newLength <= 0) return Empty;
        return new TextSegment(text, newStart, newLength);
    }

    /// <summary>
    /// Determines whether this segment starts with the specified segment (ordinal comparison).
    /// </summary>
    /// <param name="segment">The prefix to check for.</param>
    /// <returns>True if this segment starts with <paramref name="segment"/> or if <paramref name="segment"/> is empty; otherwise, false.</returns>
    public bool StartsWith(TextSegment segment)
    {
        if (segment.length == 0) return true; // Same as string.StartsWith/EndsWith("").
        if (segment.length > length) return false;
        return string.CompareOrdinal(text, startIndex, segment.text, segment.startIndex, segment.length) == 0;
    }

    /// <summary>
    /// Determines whether this segment ends with the specified segment (ordinal comparison).
    /// </summary>
    /// <param name="segment">The suffix to check for.</param>
    /// <returns>True if this segment ends with <paramref name="segment"/> or if <paramref name="segment"/> is empty; otherwise, false.</returns>
    public bool EndsWith(TextSegment segment)
    {
        if (segment.length == 0) return true; // Same as string.StartsWith/EndsWith("").
        if (segment.length > length) return false;
        return string.CompareOrdinal(text, startIndex + length - segment.length, segment.text, segment.startIndex, segment.length) == 0;
    }

    /// <summary>
    /// Determines whether this segment contains the specified segment (ordinal comparison).
    /// </summary>
    /// <param name="segment">The segment to search for.</param>
    /// <returns>True if <paramref name="segment"/> occurs within this segment or is empty; otherwise, false.</returns>
    public bool Contains(TextSegment segment) => TryFindIndex(segment, out _);

    /// <summary>
    /// Enumerator for splitting a <see cref="TextSegment"/> by a separator character.
    /// Implements both IEnumerator and IEnumerable for single-use enumeration.
    /// </summary>
    public struct SplitEnumerator : IEnumerator<TextSegment>, IEnumerable<TextSegment>
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() { }

        /// <summary>
        /// Advances the enumerator to the next segment.
        /// </summary>
        /// <returns>True if a segment is found; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            remaining = original;
            current = TextSegment.Empty;
            finished = false;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the segments.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerator<TextSegment> GetEnumerator() => this;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator IEnumerable.GetEnumerator() => this;
    }

    /// <summary>
    /// Enumerates the segment by splitting it at each occurrence of a separator character.
    /// Returns a single-use enumerable.
    /// </summary>
    /// <param name="separator">The character to split on.</param>
    /// <param name="skipEmpty">Whether to skip empty segments.</param>
    /// <returns>An enumerable of <see cref="TextSegment"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SplitEnumerator Split(char separator, bool skipEmpty = false)
    {
        return new SplitEnumerator(this, separator, skipEmpty);
    }

    /// <summary>
    /// Returns an enumerator that iterates through the characters in the segment.
    /// </summary>
    /// <returns>An enumerator for the characters in the segment.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IEnumerator<char> GetEnumerator()
    {
        for (int i = 0; i < length; i++)
        {
            yield return text[startIndex + i];
        }
    }

    /// <summary>
    /// Returns an enumerator that iterates through the characters in the segment (non-generic version).
    /// </summary>
    /// <returns>An enumerator for the characters in the segment.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Implicit conversion from <see cref="string"/> to <see cref="TextSegment"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator TextSegment(string text) => new TextSegment(text);

    /// <summary>
    /// Implicit conversion from <see cref="TextSegment"/> to <see cref="string"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator string(TextSegment textSegment) => textSegment.ToString();

    /// <summary>
    /// Determines whether two <see cref="TextSegment"/> instances are equal.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(TextSegment left, TextSegment right)
    {
        if (left.length != right.length) return false;
        // Only use cached hash codes as a shortcut; computing them here would cost more than a direct comparison.
        if (left.hashCode.HasValue && right.hashCode.HasValue && left.hashCode.Value != right.hashCode.Value) return false;

        if (left.length == 0) return true;
#if !NETSTANDARD2_0
        return left.AsSpan().SequenceEqual(right.AsSpan());
#else
        return string.CompareOrdinal(left.text, left.startIndex, right.text, right.startIndex, left.length) == 0;
#endif
    }

    /// <summary>
    /// Determines whether two <see cref="TextSegment"/> instances are not equal.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(TextSegment left, TextSegment right) => !(left == right);

    /// <summary>
    /// Determines whether this segment is equal to another <see cref="TextSegment"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(TextSegment other) => this == other;

    /// <summary>
    /// Determines whether this segment is equal to a <see cref="string"/> (ordinal comparison).
    /// </summary>
    /// <param name="other">The string to compare with. A null string is never equal.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(string other) => other != null && this == new TextSegment(other);

    /// <summary>
    /// Determines whether this segment is equal to another object.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object obj)
    {
        if (obj is TextSegment textSegment) return this == textSegment;
        if (obj is string str) return this == new TextSegment(str);
        return false;
    }

    /// <summary>
    /// Returns a hash code for this segment. The hash code is cached after the first calculation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        if (!hashCode.HasValue) hashCode = ComputeHashCode();
        return hashCode.Value;
    }

    /// <summary>
    /// Computes a hash code for the segment.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ComputeHashCode()
    {
        // Initial hash values.
        int hash1 = 5381;
        int hash2 = 5381;

        // Even indexed characters go to hash1, odd indexed ones to hash2 (processed pairwise to avoid branching).
        string s = text;
        int pos = startIndex;
        int pairEnd = startIndex + (length & ~1);
        for (; pos < pairEnd; pos += 2)
        {
            hash1 = ((hash1 << 5) + hash1) ^ s[pos];
            hash2 = ((hash2 << 5) + hash2) ^ s[pos + 1];
        }
        if ((length & 1) != 0) hash1 = ((hash1 << 5) + hash1) ^ s[pos];

        // Combining the hash values.
        return hash1 + (hash2 * 1566083941);
    }

    /// <summary>
    /// Returns the type code for this instance.
    /// </summary>
    /// <returns>The <see cref="TypeCode"/> for the underlying type, which is <see cref="TypeCode.String"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TypeCode GetTypeCode() => TypeCode.String;

    /// <summary>
    /// Converts the value of this instance to an equivalent Boolean value using the specified culture-specific formatting information.
    /// </summary>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <returns>A Boolean value equivalent to the value of this instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ToBoolean(IFormatProvider provider)
    {
#if !NETSTANDARD2_0
        return bool.Parse(AsSpan());
#else
        return bool.Parse(ToString());
#endif
    }

    /// <summary>
    /// Converts the value of this instance to an equivalent 8-bit unsigned integer using the specified culture-specific formatting information.
    /// </summary>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <returns>An 8-bit unsigned integer equivalent to the value of this instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte ToByte(IFormatProvider provider)
    {
#if !NETSTANDARD2_0
        return byte.Parse(AsSpan(), NumberStyles.Integer, provider);
#else
        return byte.Parse(ToString(), provider);
#endif
    }

    /// <summary>
    /// Converts the value of this instance to an equivalent Unicode character using the specified culture-specific formatting information.
    /// </summary>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <returns>A Unicode character equivalent to the value of this instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public char ToChar(IFormatProvider provider)
    {
#if !NETSTANDARD2_0
        var span = AsSpan();
        if (span.Length == 1) return span[0];
        throw new FormatException("TextSegment does not contain exactly one character.");
#else
        var str = ToString();
        if (str.Length == 1) return str[0];
        throw new FormatException("TextSegment does not contain exactly one character.");
#endif
    }

    /// <summary>
    /// Converts the value of this instance to an equivalent <see cref="DateTime"/> value using the specified culture-specific formatting information.
    /// </summary>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <returns>A <see cref="DateTime"/> value equivalent to the value of this instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DateTime ToDateTime(IFormatProvider provider)
    {
#if !NETSTANDARD2_0
        return DateTime.Parse(AsSpan(), provider);
#else
        return DateTime.Parse(ToString(), provider);
#endif
    }

    /// <summary>
    /// Converts the value of this instance to an equivalent decimal number using the specified culture-specific formatting information.
    /// </summary>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <returns>A decimal number equivalent to the value of this instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public decimal ToDecimal(IFormatProvider provider)
    {
#if !NETSTANDARD2_0
        return decimal.Parse(AsSpan(), NumberStyles.Number, provider);
#else
        return decimal.Parse(ToString(), provider);
#endif
    }

    /// <summary>
    /// Converts the value of this instance to an equivalent double-precision floating-point number using the specified culture-specific formatting information.
    /// </summary>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <returns>A double-precision floating-point number equivalent to the value of this instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ToDouble(IFormatProvider provider)
    {
#if !NETSTANDARD2_0
        return double.Parse(AsSpan(), NumberStyles.Float, provider);
#else
        return double.Parse(ToString(), provider);
#endif
    }

    /// <summary>
    /// Converts the value of this instance to an equivalent 16-bit signed integer using the specified culture-specific formatting information.
    /// </summary>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <returns>A 16-bit signed integer equivalent to the value of this instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short ToInt16(IFormatProvider provider)
    {
#if !NETSTANDARD2_0
        return short.Parse(AsSpan(), NumberStyles.Integer, provider);
#else
        return short.Parse(ToString(), provider);
#endif
    }

    /// <summary>
    /// Converts the value of this instance to an equivalent 32-bit signed integer using the specified culture-specific formatting information.
    /// </summary>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <returns>A 32-bit signed integer equivalent to the value of this instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ToInt32(IFormatProvider provider)
    {
#if !NETSTANDARD2_0
        return int.Parse(AsSpan(), NumberStyles.Integer, provider);
#else
        return int.Parse(ToString(), provider);
#endif
    }

    /// <summary>
    /// Converts the value of this instance to an equivalent 64-bit signed integer using the specified culture-specific formatting information.
    /// </summary>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <returns>A 64-bit signed integer equivalent to the value of this instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ToInt64(IFormatProvider provider)
    {
#if !NETSTANDARD2_0
        return long.Parse(AsSpan(), NumberStyles.Integer, provider);
#else
        return long.Parse(ToString(), provider);
#endif
    }

    /// <summary>
    /// Converts the value of this instance to an equivalent 8-bit signed integer using the specified culture-specific formatting information.
    /// </summary>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <returns>An 8-bit signed integer equivalent to the value of this instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte ToSByte(IFormatProvider provider)
    {
#if !NETSTANDARD2_0
        return sbyte.Parse(AsSpan(), NumberStyles.Integer, provider);
#else
        return sbyte.Parse(ToString(), provider);
#endif
    }

    /// <summary>
    /// Converts the value of this instance to an equivalent single-precision floating-point number using the specified culture-specific formatting information.
    /// </summary>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <returns>A single-precision floating-point number equivalent to the value of this instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ToSingle(IFormatProvider provider)
    {
#if !NETSTANDARD2_0
        return float.Parse(AsSpan(), NumberStyles.Float, provider);
#else
        return float.Parse(ToString(), provider);
#endif
    }

    /// <summary>
    /// Converts the value of this instance to an equivalent string using the specified culture-specific formatting information.
    /// </summary>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <returns>A string equivalent to the value of this instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ToString(IFormatProvider provider)
    {
        return this.ToString();
    }

    /// <summary>
    /// Converts the value of this instance to the specified type using the specified culture-specific formatting information.
    /// </summary>
    /// <param name="conversionType">The type to convert the value to.</param>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <returns>An object of the specified type whose value is equivalent to the value of this instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object ToType(Type conversionType, IFormatProvider provider)
    {
        if (conversionType == null) throw new ArgumentNullException(nameof(conversionType));
        if (TryToType(conversionType, out object result, provider)) return result;
        else return null;
    }

    /// <summary>
    /// Tries to convert the current <see cref="TextSegment"/> to the specified type.
    /// Handles all supported primitive types, their nullable counterparts, and string.
    /// For nullable types, returns <c>null</c> if the segment is empty or invalid.
    /// For unsupported types, falls back to <see cref="Convert.ChangeType(object, Type, IFormatProvider)"/>.
    /// </summary>
    /// <param name="conversionType">The target type to convert to.</param>
    /// <param name="result">The converted value, or <c>null</c> if conversion fails or the segment is empty/invalid for nullable types.</param>
    /// <param name="provider">The format provider to use for parsing (optional).</param>
    /// <returns><c>true</c> if conversion succeeded; otherwise, <c>false</c>.</returns>
    public bool TryToType(Type conversionType, out object result, IFormatProvider provider = null)
    {
        if (conversionType == null) throw new ArgumentNullException(nameof(conversionType));
        try
        {
            if (conversionType.IsNullable())
            {
                if (conversionType == typeof(string))
                {
                    if (IsValid) result = ToString(provider);
                    else result = null; // If the segment is invalid, return null for string conversion
                    return true;
                }
                if (conversionType == typeof(bool?))
                {
                    if (IsEmptyOrInvalid) { result = null; return true; }
                    result = ToBoolean(provider);
                    return true;
                }
                if (conversionType == typeof(byte?))
                {
                    if (IsEmptyOrInvalid) { result = null; return true; }
                    result = ToByte(provider);
                    return true;
                }
                if (conversionType == typeof(char?))
                {
                    if (IsEmptyOrInvalid) { result = null; return true; }
                    result = ToChar(provider);
                    return true;
                }
                if (conversionType == typeof(DateTime?))
                {
                    if (IsEmptyOrInvalid) { result = null; return true; }
                    result = ToDateTime(provider);
                    return true;
                }
                if (conversionType == typeof(decimal?))
                {
                    if (IsEmptyOrInvalid) { result = null; return true; }
                    result = ToDecimal(provider);
                    return true;
                }
                if (conversionType == typeof(double?))
                {
                    if (IsEmptyOrInvalid) { result = null; return true; }
                    result = ToDouble(provider);
                    return true;
                }
                if (conversionType == typeof(short?))
                {
                    if (IsEmptyOrInvalid) { result = null; return true; }
                    result = ToInt16(provider);
                    return true;
                }
                if (conversionType == typeof(int?))
                {
                    if (IsEmptyOrInvalid) { result = null; return true; }
                    result = ToInt32(provider);
                    return true;
                }
                if (conversionType == typeof(long?))
                {
                    if (IsEmptyOrInvalid) { result = null; return true; }
                    result = ToInt64(provider);
                    return true;
                }
                if (conversionType == typeof(sbyte?))
                {
                    if (IsEmptyOrInvalid) { result = null; return true; }
                    result = ToSByte(provider);
                    return true;
                }
                if (conversionType == typeof(float?))
                {
                    if (IsEmptyOrInvalid) { result = null; return true; }
                    result = ToSingle(provider);
                    return true;
                }
                if (conversionType == typeof(ushort?))
                {
                    if (IsEmptyOrInvalid) { result = null; return true; }
                    result = ToUInt16(provider);
                    return true;
                }
                if (conversionType == typeof(uint?))
                {
                    if (IsEmptyOrInvalid) { result = null; return true; }
                    result = ToUInt32(provider);
                    return true;
                }
                if (conversionType == typeof(ulong?))
                {
                    if (IsEmptyOrInvalid) { result = null; return true; }
                    result = ToUInt64(provider);
                    return true;
                }
            }

            // Handle non-nullable types and fallback.
            if (conversionType == typeof(string))
            {
                if (IsValid) result = ToString(provider);
                else result = null; // If the segment is invalid, return null for string conversion                
            }
            else if (conversionType == typeof(TextSegment)) result = this;
            else if (IsEmptyOrInvalid)
            {
                result = null;
                return false;
            }
            else if (conversionType == typeof(bool)) result = ToBoolean(provider);
            else if (conversionType == typeof(byte)) result = ToByte(provider);
            else if (conversionType == typeof(char)) result = ToChar(provider);
            else if (conversionType == typeof(DateTime)) result = ToDateTime(provider);
            else if (conversionType == typeof(decimal)) result = ToDecimal(provider);
            else if (conversionType == typeof(double)) result = ToDouble(provider);
            else if (conversionType == typeof(short)) result = ToInt16(provider);
            else if (conversionType == typeof(int)) result = ToInt32(provider);
            else if (conversionType == typeof(long)) result = ToInt64(provider);
            else if (conversionType == typeof(sbyte)) result = ToSByte(provider);
            else if (conversionType == typeof(float)) result = ToSingle(provider);
            else if (conversionType == typeof(ushort)) result = ToUInt16(provider);
            else if (conversionType == typeof(uint)) result = ToUInt32(provider);
            else if (conversionType == typeof(ulong)) result = ToUInt64(provider);
            else
            {
                // Fallback for other types (may box)
                result = Convert.ChangeType(ToString(), conversionType, provider);
            }
            return true;
        }
        catch
        {
            result = null;
            return false;
        }
    }

    /// <summary>
    /// Tries to convert the current <see cref="TextSegment"/> to the specified type.
    /// Handles all supported primitive types, their nullable counterparts, and string.
    /// For nullable types, returns <c>null</c> if the segment is empty or invalid.
    /// For unsupported types, falls back to <see cref="Convert.ChangeType(object, Type, IFormatProvider)"/>.
    /// </summary>
    /// <param name="result">The converted value, or <c>null</c> if conversion fails or the segment is empty/invalid for nullable types.</param>
    /// <param name="provider">The format provider to use for parsing (optional).</param>
    /// <returns><c>true</c> if conversion succeeded; otherwise, <c>false</c>.</returns>
    public bool TryToType<T>(out T result, IFormatProvider provider = null)
    {
        // Safety note for Unsafe.As usage in this method:
        // - Every Unsafe.As<TFrom, T>(ref x) call is gated by an exact runtime type check
        //   (e.g. `type == typeof(int)`), so that branch executes only when `T` is exactly that type.
        // - Therefore `TFrom` and `T` have identical runtime type/layout in the executed branch, and
        //   Unsafe.As is used as a zero-allocation typed return path (no cross-type reinterpretation).
        try
        {
            Type type = typeof(T);
            
            if (type == typeof(string))
            {
                var x = ToString(provider);
                result = System.Runtime.CompilerServices.Unsafe.As<string, T>(ref x);
            }
            else if (type == typeof(TextSegment))
            {
                var x = this;
                result = System.Runtime.CompilerServices.Unsafe.As<TextSegment, T>(ref x);
            }
            else if (type == typeof(bool))
            {
                var x = ToBoolean(provider);
                result = System.Runtime.CompilerServices.Unsafe.As<bool, T>(ref x);
            }
            else if (type == typeof(byte))
            {
                var x = ToByte(provider);
                result = System.Runtime.CompilerServices.Unsafe.As<byte, T>(ref x);
            }
            else if (type == typeof(char))
            {
                var x = ToChar(provider);
                result = System.Runtime.CompilerServices.Unsafe.As<char, T>(ref x);
            }
            else if (type == typeof(DateTime))
            {
                var x = ToDateTime(provider);
                result = System.Runtime.CompilerServices.Unsafe.As<DateTime, T>(ref x);
            }
            else if (type == typeof(decimal))
            {
                var x = ToDecimal(provider);
                result = System.Runtime.CompilerServices.Unsafe.As<decimal, T>(ref x);
            }
            else if (type == typeof(double))
            {
                var x = ToDouble(provider);
                result = System.Runtime.CompilerServices.Unsafe.As<double, T>(ref x);
            }
            else if (type == typeof(short))
            {
                var x = ToInt16(provider);
                result = System.Runtime.CompilerServices.Unsafe.As<short, T>(ref x);
            }
            else if (type == typeof(int))
            {
                var x = ToInt32(provider);
                result = System.Runtime.CompilerServices.Unsafe.As<int, T>(ref x);
            }
            else if (type == typeof(long))
            {
                var x = ToInt64(provider);
                result = System.Runtime.CompilerServices.Unsafe.As<long, T>(ref x);
            }
            else if (type == typeof(sbyte))
            {
                var x = ToSByte(provider);
                result = System.Runtime.CompilerServices.Unsafe.As<sbyte, T>(ref x);
            }
            else if (type == typeof(float))
            {
                var x = ToSingle(provider);
                result = System.Runtime.CompilerServices.Unsafe.As<float, T>(ref x);
            }
            else if (type == typeof(ushort))
            {
                var x = ToUInt16(provider);
                result = System.Runtime.CompilerServices.Unsafe.As<ushort, T>(ref x);
            }
            else if (type == typeof(uint))
            {
                var x = ToUInt32(provider);
                result = System.Runtime.CompilerServices.Unsafe.As<uint, T>(ref x);
            }
            else if (type == typeof(ulong))
            {
                var x = ToUInt64(provider);
                result = System.Runtime.CompilerServices.Unsafe.As<ulong, T>(ref x);
            }
            // Handle nullable types
            else if (type == typeof(TextSegment?))
            {
                if (this.IsEmptyOrInvalid) result = default;
                else
                {
                    TextSegment? x = this;
                    result = System.Runtime.CompilerServices.Unsafe.As<TextSegment?, T>(ref x);
                }
            }
            else if (type == typeof(bool?))
            {
                if (this.IsEmptyOrInvalid) result = default;
                else
                {
                    bool? x = ToBoolean(provider);
                    result = System.Runtime.CompilerServices.Unsafe.As<bool?, T>(ref x);
                }
            }
            else if (type == typeof(byte?))
            {
                if (this.IsEmptyOrInvalid) result = default;
                else
                {
                    byte? x = ToByte(provider);
                    result = System.Runtime.CompilerServices.Unsafe.As<byte?, T>(ref x);
                }
            }
            else if (type == typeof(char?))
            {
                if (this.IsEmptyOrInvalid) result = default;
                else
                {
                    char? x = ToChar(provider);
                    result = System.Runtime.CompilerServices.Unsafe.As<char?, T>(ref x);
                }
            }
            else if (type == typeof(DateTime?))
            {
                if (this.IsEmptyOrInvalid) result = default;
                else
                {
                    DateTime? x = ToDateTime(provider);
                    result = System.Runtime.CompilerServices.Unsafe.As<DateTime?, T>(ref x);
                }
            }
            else if (type == typeof(decimal?))
            {
                if (this.IsEmptyOrInvalid) result = default;
                else
                {
                    decimal? x = ToDecimal(provider);
                    result = System.Runtime.CompilerServices.Unsafe.As<decimal?, T>(ref x);
                }
            }
            else if (type == typeof(double?))
            {
                if (this.IsEmptyOrInvalid) result = default;
                else
                {
                    double? x = ToDouble(provider);
                    result = System.Runtime.CompilerServices.Unsafe.As<double?, T>(ref x);
                }
            }
            else if (type == typeof(short?))
            {
                if (this.IsEmptyOrInvalid) result = default;
                else
                {
                    short? x = ToInt16(provider);
                    result = System.Runtime.CompilerServices.Unsafe.As<short?, T>(ref x);
                }
            }
            else if (type == typeof(int?))
            {
                if (this.IsEmptyOrInvalid) result = default;
                else
                {
                    int? x = ToInt32(provider);
                    result = System.Runtime.CompilerServices.Unsafe.As<int?, T>(ref x);
                }
            }
            else if (type == typeof(long?))
            {
                if (this.IsEmptyOrInvalid) result = default;
                else
                {
                    long? x = ToInt64(provider);
                    result = System.Runtime.CompilerServices.Unsafe.As<long?, T>(ref x);
                }
            }
            else if (type == typeof(sbyte?))
            {
                if (this.IsEmptyOrInvalid) result = default;
                else
                {
                    sbyte? x = ToSByte(provider);
                    result = System.Runtime.CompilerServices.Unsafe.As<sbyte?, T>(ref x);
                }
            }
            else if (type == typeof(float?))
            {
                if (this.IsEmptyOrInvalid) result = default;
                else
                {
                    float? x = ToSingle(provider);
                    result = System.Runtime.CompilerServices.Unsafe.As<float?, T>(ref x);
                }
            }
            else if (type == typeof(ushort?))
            {
                if (this.IsEmptyOrInvalid) result = default;
                else
                {
                    ushort? x = ToUInt16(provider);
                    result = System.Runtime.CompilerServices.Unsafe.As<ushort?, T>(ref x);
                }
            }
            else if (type == typeof(uint?))
            {
                if (this.IsEmptyOrInvalid) result = default;
                else
                {
                    uint? x = ToUInt32(provider);
                    result = System.Runtime.CompilerServices.Unsafe.As<uint?, T>(ref x);
                }
            }
            else if (type == typeof(ulong?))
            {
                if (this.IsEmptyOrInvalid) result = default;
                else
                {
                    ulong? x = ToUInt64(provider);
                    result = System.Runtime.CompilerServices.Unsafe.As<ulong?, T>(ref x);
                }
            }
            else
            {
                // Fallback for other types (will box)
                result = (T)Convert.ChangeType(ToString(), typeof(T), provider);
            }
            return true;
        }
        catch
        {
            result = default;
            return false;
        }
    }

    /// <summary>
    /// Converts the value of this instance to an equivalent 16-bit unsigned integer using the specified culture-specific formatting information.
    /// </summary>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <returns>A 16-bit unsigned integer equivalent to the value of this instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort ToUInt16(IFormatProvider provider)
    {
#if !NETSTANDARD2_0
        return ushort.Parse(AsSpan(), NumberStyles.Integer, provider);
#else
        return ushort.Parse(ToString(), provider);
#endif
    }

    /// <summary>
    /// Converts the value of this instance to an equivalent 32-bit unsigned integer using the specified culture-specific formatting information.
    /// </summary>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <returns>A 32-bit unsigned integer equivalent to the value of this instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ToUInt32(IFormatProvider provider)
    {
#if !NETSTANDARD2_0
        return uint.Parse(AsSpan(), NumberStyles.Integer, provider);
#else
        return uint.Parse(ToString(), provider);
#endif
    }

    /// <summary>
    /// Converts the value of this instance to an equivalent 64-bit unsigned integer using the specified culture-specific formatting information.
    /// </summary>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <returns>A 64-bit unsigned integer equivalent to the value of this instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong ToUInt64(IFormatProvider provider)
    {
#if !NETSTANDARD2_0
        return ulong.Parse(AsSpan(), NumberStyles.Integer, provider);
#else
        return ulong.Parse(ToString(), provider);
#endif
    }
}
