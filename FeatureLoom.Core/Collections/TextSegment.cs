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
    /// Returns a subsegment of this <see cref="TextSegment"/> that starts after the specified <paramref name="startAfter"/> segment
    /// and ends before the specified <paramref name="endBefore"/> segment.
    /// </summary>
    /// <param name="startIndex">The starting index to begin searching from, relative to this segment.</param>
    /// <param name="startAfter">The segment after which the subsegment should start. If empty or null, starts from <paramref name="startIndex"/>.</param>
    /// <param name="endBefore">The segment before which the subsegment should end. If empty or null, ends at the end of this segment.</param>
    /// <param name="restStartIndex">Outputs the index after the end of the subsegment, relative to this segment.</param>
    /// <param name="includeSearchStrings">If true, includes the search strings in the result.</param>
    /// <returns>
    /// A <see cref="TextSegment"/> representing the subsegment, or null if not found.
    /// </returns>
    public TextSegment? SubSegment(int startIndex, TextSegment startAfter, TextSegment endBefore, out int restStartIndex, bool includeSearchStrings = false)
    {
        int startPos = startIndex;
        int endPos = Count;

        // Find startAfter
        if (!startAfter.IsEmptyOrInvalid)
        {
            if (!TryFindIndex(startAfter, startIndex, out int foundStart))
            {
                restStartIndex = startIndex;
                return null;
            }
            startPos = foundStart + startAfter.Count;
            if (includeSearchStrings) startPos -= startAfter.Count;
        }

        // Find endBefore
        int foundEnd = -1;
        if (!endBefore.IsEmptyOrInvalid)
        {
            if (!TryFindIndex(endBefore, startPos, out foundEnd))
            {
                restStartIndex = startPos;
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
        for (int i = 0; i < length; i++)
        {
            if (this[i] == c) return true;
        }
        return false;
    }

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
    /// Tries to find the index of the first occurrence of another <see cref="TextSegment"/> within this segment.
    /// </summary>
    /// <param name="other">The segment to search for.</param>
    /// <param name="firstIndex">The index where to start from</param>
    /// <param name="index">The index of the first occurrence, if found.</param>
    /// <returns>True if found; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFindIndex(TextSegment other, int firstIndex, out int index)
    {
        for (index = firstIndex; index < length; index++)
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
            if (this[index] == c) return true;
        }
        return false;
    }

    /// <summary>
    /// Tries to find the index of the first occurrence of a character within this segment.
    /// </summary>
    /// <param name="c">The character to search for.</param>
    /// <param name="firstIndex">The index where to start from</param>
    /// <param name="index">The index of the first occurrence, if found.</param>
    /// <returns>True if found; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFindIndex(char c, int firstIndex, out int index)
    {
        for (index = firstIndex; index < length; index++)
        {
            if (this[index] == c) return true;
        }
        return false;
    }

    /// <summary>
    /// Tries to find the index of the last occurrence of another <see cref="TextSegment"/> within this segment.
    /// </summary>
    /// <param name="other">The segment to search for.</param>
    /// <param name="index">The index of the last occurrence, if found.</param>
    /// <returns>True if found; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFindLastIndex(TextSegment other, out int index)
    {
        if (other.length == 0 || other.length > length)
        {
            index = -1;
            return false;
        }

        for (index = length - other.length; index >= 0; index--)
        {
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
        index = -1;
        return false;
    }

    /// <summary>
    /// Tries to find the index of the last occurrence of another <see cref="TextSegment"/> within this segment,
    /// searching backward from <paramref name="lastIndex"/>. The entire <paramref name="other"/> segment
    /// must fit within the range [0, lastIndex].
    /// </summary>
    /// <param name="other">The segment to search for.</param>
    /// <param name="lastIndex">The index to start searching backward from. The last character of <paramref name="other"/> must be at or before this index.</param>
    /// <param name="index">The index of the last occurrence, if found.</param>
    /// <returns>True if found; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFindLastIndex(TextSegment other, int lastIndex, out int index)
    {
        if (other.length == 0 || other.length > length)
        {
            index = -1;
            return false;
        }

        int maxStart = Math.Min(lastIndex - other.length + 1, length - other.length);
        if (maxStart < 0)
        {
            index = -1;
            return false;
        }

        var searchSegment = SubSegment(0, maxStart + other.length);
        return searchSegment.TryFindLastIndex(other, out index);
    }

    /// <summary>
    /// Tries to find the index of the last occurrence of a character within this segment.
    /// </summary>
    /// <param name="c">The character to search for.</param>
    /// <param name="index">The index of the last occurrence, if found.</param>
    /// <returns>True if found; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFindLastIndex(char c, out int index)
    {
        for (index = length - 1; index >= 0; index--)
        {
            if (this[index] == c) return true;
        }
        index = -1;
        return false;
    }

    /// <summary>
    /// Tries to find the index of the last occurrence of a character within this segment, searching backward from a given index.
    /// </summary>
    /// <param name="c">The character to search for.</param>
    /// <param name="lastIndex">The index to start searching backward from.</param>
    /// <param name="index">The index of the last occurrence, if found.</param>
    /// <returns>True if found; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFindLastIndex(char c, int lastIndex, out int index)
    {
        int start = Math.Min(lastIndex, length - 1);
        for (index = start; index >= 0; index--)
        {
            if (this[index] == c) return true;
        }
        index = -1;
        return false;
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryExtract<T>(int startIndex, TextSegment startExtractAfter, TextSegment endExtractBefore, out T extract, out int restStartIndex, bool includeSearchStrings = false) where T : IConvertible
    {
        extract = default;
        var subsegment = SubSegment(startIndex, startExtractAfter, endExtractBefore, out restStartIndex, includeSearchStrings);
        if (subsegment == null) return false;        
        return subsegment.Value.TryToType(out extract, CultureInfo.InvariantCulture);
    }

    public bool TryExtract<T>(TextSegment startExtractAfter, TextSegment endExtractBefore, out T extract) where T : IConvertible
    {
        return TryExtract(0, startExtractAfter, endExtractBefore, out extract, out _);
    }

    public bool TryExtract<T>(int startIndex, TextSegment startExtractAfter, TextSegment endExtractBefore, out T extract) where T : IConvertible
    {
        return TryExtract(startIndex, startExtractAfter, endExtractBefore, out extract, out _);
    }

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
    /// Returns a new <see cref="TextSegment"/> with all leading characters equal to <paramref name="trimChars"/> removed.
    /// </summary>
    public TextSegment Trim(params char[] trimChars)
    {
        if (IsEmptyOrInvalid || trimChars == null || trimChars.Length == 0) return this;

        int newStart = startIndex;
        int newEnd = startIndex + length - 1;

        // Trim start
        while (newStart <= newEnd && trimChars.Contains(text[newStart]))
        {
            newStart++;
        }

        // Trim end
        while (newEnd >= newStart && trimChars.Contains(text[newEnd]))
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
        while (newStart <= newEnd && trimChars.Contains(text[newStart]))
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
        while (newEnd >= newStart && trimChars.Contains(text[newEnd]))
        {
            newEnd--;
        }

        int newLength = newEnd - newStart + 1;
        if (newLength <= 0) return Empty;

        return new TextSegment(text, newStart, newLength);
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

    public bool StartsWith(TextSegment segment) => TryFindIndex(segment, out int index) && index == 0;
    public bool EndsWith(TextSegment segment) => TryFindIndex(segment, out int index) && index + segment.length == length;

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(TextSegment left, TextSegment right) => !(left == right);

    /// <summary>
    /// Determines whether this segment is equal to another <see cref="TextSegment"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(TextSegment other) => this == other;

    /// <summary>
    /// Determines whether this segment is equal to a <see cref="string"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(string other) => this == new TextSegment(other);

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
                    var x = this;
                    result = System.Runtime.CompilerServices.Unsafe.As<TextSegment, T>(ref x);
                }                
            }
            else if (type == typeof(bool?))
            {
                if (this.IsEmptyOrInvalid) result = default;
                else
                {
                    var x = ToBoolean(provider);
                    result = System.Runtime.CompilerServices.Unsafe.As<bool, T>(ref x);
                }                
            }
            else if (type == typeof(byte?))
            {
                if (this.IsEmptyOrInvalid) result = default;
                else
                {
                    var x = ToByte(provider);
                    result = System.Runtime.CompilerServices.Unsafe.As<byte, T>(ref x);
                }                
            }
            else if (type == typeof(char?))
            {
                if (this.IsEmptyOrInvalid) result = default;
                else
                {
                    var x = ToChar(provider);
                    result = System.Runtime.CompilerServices.Unsafe.As<char, T>(ref x);
                }
            }
            else if (type == typeof(DateTime?))
            {
                if (this.IsEmptyOrInvalid) result = default;
                else
                {
                    var x = ToDateTime(provider);
                    result = System.Runtime.CompilerServices.Unsafe.As<DateTime, T>(ref x);
                }
            }
            else if (type == typeof(decimal?))
            {
                if (this.IsEmptyOrInvalid) result = default;
                else
                {
                    var x = ToDecimal(provider);
                    result = System.Runtime.CompilerServices.Unsafe.As<decimal, T>(ref x);
                }                
            }
            else if (type == typeof(double?))
            {
                if (this.IsEmptyOrInvalid) result = default;
                else
                {
                    var x = ToDouble(provider);
                    result = System.Runtime.CompilerServices.Unsafe.As<double, T>(ref x);
                }
            }
            else if (type == typeof(short?))
            {
                if (this.IsEmptyOrInvalid) result = default;
                else
                {
                    var x = ToInt16(provider);
                    result = System.Runtime.CompilerServices.Unsafe.As<short, T>(ref x);
                }                
            }
            else if (type == typeof(int?))
            {
                if (this.IsEmptyOrInvalid) result = default;
                else
                {
                    var x = ToInt32(provider);
                    result = System.Runtime.CompilerServices.Unsafe.As<int, T>(ref x);
                }
            }
            else if (type == typeof(long?))
            {
                if (this.IsEmptyOrInvalid) result = default;
                else
                {
                    var x = ToInt64(provider);
                    result = System.Runtime.CompilerServices.Unsafe.As<long, T>(ref x);
                }
            }
            else if (type == typeof(sbyte?))
            {
                if (this.IsEmptyOrInvalid) result = default;
                else
                {
                    var x = ToSByte(provider);
                    result = System.Runtime.CompilerServices.Unsafe.As<sbyte, T>(ref x);
                }
            }
            else if (type == typeof(float?))
            {
                if (this.IsEmptyOrInvalid) result = default;
                else
                {
                    var x = ToSingle(provider);
                    result = System.Runtime.CompilerServices.Unsafe.As<float, T>(ref x);
                }
            }
            else if (type == typeof(ushort?))
            {
                if (this.IsEmptyOrInvalid) result = default;
                else
                {
                    var x = ToUInt16(provider);
                    result = System.Runtime.CompilerServices.Unsafe.As<ushort, T>(ref x);
                }
            }
            else if (type == typeof(uint?))
            {
                if (this.IsEmptyOrInvalid) result = default;
                else
                {
                    var x = ToUInt32(provider);
                    result = System.Runtime.CompilerServices.Unsafe.As<uint, T>(ref x);
                }
            }
            else if (type == typeof(ulong?))
            {
                if (this.IsEmptyOrInvalid) result = default;
                else
                {
                    var x = ToUInt64(provider);
                    result = System.Runtime.CompilerServices.Unsafe.As<ulong, T>(ref x);
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
