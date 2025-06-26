using FeatureLoom.Extensions;
using FeatureLoom.Collections;
using System;
using System.Collections.Generic;

namespace FeatureLoom.Helpers;

/// <summary>
/// Extracts values from a text segment based on a pattern with placeholders (e.g., "prefix{value1}middle{value2}suffix").
/// Utilizes <see cref="TextSegment"/> for efficient, non-allocating parsing and extraction.
/// </summary>
public class PatternExtractor
{
    readonly List<TextSegment> staticParts = new List<TextSegment>();
    int size = 0;
    TextSegment pattern;

    /// <summary>
    /// Gets the number of placeholders in the pattern.
    /// </summary>
    public int Size => size;

    /// <summary>
    /// Gets the pattern used by this extractor.
    /// </summary>
    public TextSegment Pattern => pattern;

    /// <summary>
    /// Initializes a new instance of the <see cref="PatternExtractor"/> class with the specified pattern.
    /// </summary>
    /// <param name="pattern">The pattern containing static text and placeholders (e.g., "prefix{value1}middle{value2}suffix").</param>
    /// <exception cref="Exception">Thrown if the pattern contains consecutive placeholders without static text between them.</exception>
    public PatternExtractor(TextSegment pattern)
    {
        Init(pattern);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PatternExtractor"/> class with the specified pattern,
    /// and outputs the first static element. Optionally removes the first static element from the extractor.
    /// </summary>
    /// <param name="pattern">The pattern containing static text and placeholders.</param>
    /// <param name="firstStaticElement">Outputs the first static element of the pattern.</param>
    /// <param name="removeFirstStaticElement">If true, removes the first static element from the extractor.</param>
    public PatternExtractor(TextSegment pattern, out TextSegment firstStaticElement, bool removeFirstStaticElement)
    {
        Init(pattern);
        if (staticParts.Count > 0)
        {
            firstStaticElement = staticParts[0];
            if (removeFirstStaticElement) staticParts[0] = TextSegment.Empty;
        }
        else
        {
            firstStaticElement = TextSegment.Empty;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PatternExtractor"/> class with the specified pattern,
    /// and outputs the first static element. Optionally removes the first static element from the extractor.
    /// </summary>
    /// <param name="pattern">The pattern containing static text and placeholders.</param>
    /// <param name="firstStaticElement">Outputs the first static element of the pattern.</param>
    /// <param name="removeFirstStaticElement">If true, removes the first static element from the extractor.</param>
    public PatternExtractor(TextSegment pattern, out string firstStaticElement, bool removeFirstStaticElement)
    {
        Init(pattern);
        if (staticParts.Count > 0)
        {
            firstStaticElement = staticParts[0];
            if (removeFirstStaticElement) staticParts[0] = TextSegment.Empty;
        }
        else
        {
            firstStaticElement = TextSegment.Empty;
        }
    }

    /// <summary>
    /// Parses the pattern and initializes the static parts and size.
    /// </summary>
    /// <param name="pattern">The pattern to parse.</param>
    /// <exception cref="Exception">Thrown if the pattern contains consecutive placeholders without static text between them.</exception>
    private void Init(TextSegment pattern)
    {
        this.pattern = pattern;
        int pos = 0;
        while (pattern.TryExtract(pos, TextSegment.Empty, "{", out TextSegment staticPart, out pos))
        {
            if (staticPart.Count == 0 && staticParts.Count > 0) throw new Exception($"Pattern ({pattern}) is invalid. There must be at least one character between two placeholders!");

            staticParts.Add(staticPart);
            size++;
            while (pattern.Count > pos && pattern[pos++] != '}') ;
        }
        if (pos == pattern.Count) staticParts.Add(TextSegment.Empty);
        else staticParts.Add(pattern.SubSegment(pos));
    }

    /// <summary>
    /// Attempts to extract a single value from the source based on the pattern.
    /// </summary>
    /// <typeparam name="T1">The type of the value to extract. Must implement <see cref="IConvertible"/>.</typeparam>
    /// <param name="source">The source text segment to extract from.</param>
    /// <param name="item1">The extracted value, if successful.</param>
    /// <returns>True if extraction succeeded; otherwise, false.</returns>
    public bool TryExtract<T1>(TextSegment source, out T1 item1)
        where T1 : IConvertible
    {
        item1 = default;
        int pos = 0;
        if (staticParts.Count <= 1) return false;
        if (!source.TryExtract(pos, staticParts[0], staticParts[1], out item1, out pos)) return false;
        return true;
    }

    /// <summary>
    /// Attempts to extract two values from the source based on the pattern.
    /// </summary>
    /// <typeparam name="T1">The type of the first value to extract.</typeparam>
    /// <typeparam name="T2">The type of the second value to extract.</typeparam>
    /// <param name="source">The source text segment to extract from.</param>
    /// <param name="item1">The first extracted value, if successful.</param>
    /// <param name="item2">The second extracted value, if successful.</param>
    /// <returns>True if extraction succeeded; otherwise, false.</returns>
    public bool TryExtract<T1, T2>(TextSegment source, out T1 item1, out T2 item2)
        where T1 : IConvertible
        where T2 : IConvertible
    {
        item1 = default;
        item2 = default;
        int pos = 0;
        if (staticParts.Count <= 2) return false;
        if (!source.TryExtract(pos, staticParts[0], staticParts[1], out item1, out pos)) return false;
        if (!source.TryExtract(pos, staticParts[1], staticParts[2], out item2, out pos)) return false;
        return true;
    }

    /// <summary>
    /// Attempts to extract three values from the source based on the pattern.
    /// </summary>
    /// <typeparam name="T1">The type of the first value to extract.</typeparam>
    /// <typeparam name="T2">The type of the second value to extract.</typeparam>
    /// <typeparam name="T3">The type of the third value to extract.</typeparam>
    /// <param name="source">The source text segment to extract from.</param>
    /// <param name="item1">The first extracted value, if successful.</param>
    /// <param name="item2">The second extracted value, if successful.</param>
    /// <param name="item3">The third extracted value, if successful.</param>
    /// <returns>True if extraction succeeded; otherwise, false.</returns>
    public bool TryExtract<T1, T2, T3>(TextSegment source, out T1 item1, out T2 item2, out T3 item3)
        where T1 : IConvertible
        where T2 : IConvertible
        where T3 : IConvertible
    {
        item1 = default;
        item2 = default;
        item3 = default;
        int pos = 0;
        if (staticParts.Count <= 3) return false;
        if (!source.TryExtract(pos, staticParts[0], staticParts[1], out item1, out pos)) return false;
        if (!source.TryExtract(pos, staticParts[1], staticParts[2], out item2, out pos)) return false;
        if (!source.TryExtract(pos, staticParts[2], staticParts[3], out item3, out pos)) return false;
        return true;
    }

    /// <summary>
    /// Attempts to extract four values from the source based on the pattern.
    /// </summary>
    /// <typeparam name="T1">The type of the first value to extract.</typeparam>
    /// <typeparam name="T2">The type of the second value to extract.</typeparam>
    /// <typeparam name="T3">The type of the third value to extract.</typeparam>
    /// <typeparam name="T4">The type of the fourth value to extract.</typeparam>
    /// <param name="source">The source text segment to extract from.</param>
    /// <param name="item1">The first extracted value, if successful.</param>
    /// <param name="item2">The second extracted value, if successful.</param>
    /// <param name="item3">The third extracted value, if successful.</param>
    /// <param name="item4">The fourth extracted value, if successful.</param>
    /// <returns>True if extraction succeeded; otherwise, false.</returns>
    public bool TryExtract<T1, T2, T3, T4>(TextSegment source, out T1 item1, out T2 item2, out T3 item3, out T4 item4)
        where T1 : IConvertible
        where T2 : IConvertible
        where T3 : IConvertible
        where T4 : IConvertible
    {
        item1 = default;
        item2 = default;
        item3 = default;
        item4 = default;
        int pos = 0;
        if (staticParts.Count <= 4) return false;
        if (!source.TryExtract(pos, staticParts[0], staticParts[1], out item1, out pos)) return false;
        if (!source.TryExtract(pos, staticParts[1], staticParts[2], out item2, out pos)) return false;
        if (!source.TryExtract(pos, staticParts[2], staticParts[3], out item3, out pos)) return false;
        if (!source.TryExtract(pos, staticParts[3], staticParts[4], out item4, out pos)) return false;
        return true;
    }

    /// <summary>
    /// Attempts to extract five values from the source based on the pattern.
    /// </summary>
    /// <typeparam name="T1">The type of the first value to extract.</typeparam>
    /// <typeparam name="T2">The type of the second value to extract.</typeparam>
    /// <typeparam name="T3">The type of the third value to extract.</typeparam>
    /// <typeparam name="T4">The type of the fourth value to extract.</typeparam>
    /// <typeparam name="T5">The type of the fifth value to extract.</typeparam>
    /// <param name="source">The source text segment to extract from.</param>
    /// <param name="item1">The first extracted value, if successful.</param>
    /// <param name="item2">The second extracted value, if successful.</param>
    /// <param name="item3">The third extracted value, if successful.</param>
    /// <param name="item4">The fourth extracted value, if successful.</param>
    /// <param name="item5">The fifth extracted value, if successful.</param>
    /// <returns>True if extraction succeeded; otherwise, false.</returns>
    public bool TryExtract<T1, T2, T3, T4, T5>(TextSegment source, out T1 item1, out T2 item2, out T3 item3, out T4 item4, out T5 item5)
        where T1 : IConvertible
        where T2 : IConvertible
        where T3 : IConvertible
        where T4 : IConvertible
        where T5 : IConvertible
    {
        item1 = default;
        item2 = default;
        item3 = default;
        item4 = default;
        item5 = default;
        int pos = 0;
        if (staticParts.Count <= 5) return false;
        if (!source.TryExtract(pos, staticParts[0], staticParts[1], out item1, out pos)) return false;
        if (!source.TryExtract(pos, staticParts[1], staticParts[2], out item2, out pos)) return false;
        if (!source.TryExtract(pos, staticParts[2], staticParts[3], out item3, out pos)) return false;
        if (!source.TryExtract(pos, staticParts[3], staticParts[4], out item4, out pos)) return false;
        if (!source.TryExtract(pos, staticParts[4], staticParts[5], out item5, out pos)) return false;
        return true;
    }

    /// <summary>
    /// Attempts to extract six values from the source based on the pattern.
    /// </summary>
    /// <typeparam name="T1">The type of the first value to extract.</typeparam>
    /// <typeparam name="T2">The type of the second value to extract.</typeparam>
    /// <typeparam name="T3">The type of the third value to extract.</typeparam>
    /// <typeparam name="T4">The type of the fourth value to extract.</typeparam>
    /// <typeparam name="T5">The type of the fifth value to extract.</typeparam>
    /// <typeparam name="T6">The type of the sixth value to extract.</typeparam>
    /// <param name="source">The source text segment to extract from.</param>
    /// <param name="item1">The first extracted value, if successful.</param>
    /// <param name="item2">The second extracted value, if successful.</param>
    /// <param name="item3">The third extracted value, if successful.</param>
    /// <param name="item4">The fourth extracted value, if successful.</param>
    /// <param name="item5">The fifth extracted value, if successful.</param>
    /// <param name="item6">The sixth extracted value, if successful.</param>
    /// <returns>True if extraction succeeded; otherwise, false.</returns>
    public bool TryExtract<T1, T2, T3, T4, T5, T6>(TextSegment source, out T1 item1, out T2 item2, out T3 item3, out T4 item4, out T5 item5, out T6 item6)
        where T1 : IConvertible
        where T2 : IConvertible
        where T3 : IConvertible
        where T4 : IConvertible
        where T5 : IConvertible
        where T6 : IConvertible
    {
        item1 = default;
        item2 = default;
        item3 = default;
        item4 = default;
        item5 = default;
        item6 = default;
        int pos = 0;
        if (staticParts.Count <= 6) return false;
        if (!source.TryExtract(pos, staticParts[0], staticParts[1], out item1, out pos)) return false;
        if (!source.TryExtract(pos, staticParts[1], staticParts[2], out item2, out pos)) return false;
        if (!source.TryExtract(pos, staticParts[2], staticParts[3], out item3, out pos)) return false;
        if (!source.TryExtract(pos, staticParts[3], staticParts[4], out item4, out pos)) return false;
        if (!source.TryExtract(pos, staticParts[4], staticParts[5], out item5, out pos)) return false;
        if (!source.TryExtract(pos, staticParts[5], staticParts[6], out item6, out pos)) return false;
        return true;
    }

    /// <summary>
    /// Attempts to extract seven values from the source based on the pattern.
    /// </summary>
    /// <typeparam name="T1">The type of the first value to extract.</typeparam>
    /// <typeparam name="T2">The type of the second value to extract.</typeparam>
    /// <typeparam name="T3">The type of the third value to extract.</typeparam>
    /// <typeparam name="T4">The type of the fourth value to extract.</typeparam>
    /// <typeparam name="T5">The type of the fifth value to extract.</typeparam>
    /// <typeparam name="T6">The type of the sixth value to extract.</typeparam>
    /// <typeparam name="T7">The type of the seventh value to extract.</typeparam>
    /// <param name="source">The source text segment to extract from.</param>
    /// <param name="item1">The first extracted value, if successful.</param>
    /// <param name="item2">The second extracted value, if successful.</param>
    /// <param name="item3">The third extracted value, if successful.</param>
    /// <param name="item4">The fourth extracted value, if successful.</param>
    /// <param name="item5">The fifth extracted value, if successful.</param>
    /// <param name="item6">The sixth extracted value, if successful.</param>
    /// <param name="item7">The seventh extracted value, if successful.</param>
    /// <returns>True if extraction succeeded; otherwise, false.</returns>
    public bool TryExtract<T1, T2, T3, T4, T5, T6, T7>(TextSegment source, out T1 item1, out T2 item2, out T3 item3, out T4 item4, out T5 item5, out T6 item6, out T7 item7)
        where T1 : IConvertible
        where T2 : IConvertible
        where T3 : IConvertible
        where T4 : IConvertible
        where T5 : IConvertible
        where T6 : IConvertible
        where T7 : IConvertible
    {
        item1 = default;
        item2 = default;
        item3 = default;
        item4 = default;
        item5 = default;
        item6 = default;
        item7 = default;
        int pos = 0;
        if (staticParts.Count <= 7) return false;
        if (!source.TryExtract(pos, staticParts[0], staticParts[1], out item1, out pos)) return false;
        if (!source.TryExtract(pos, staticParts[1], staticParts[2], out item2, out pos)) return false;
        if (!source.TryExtract(pos, staticParts[2], staticParts[3], out item3, out pos)) return false;
        if (!source.TryExtract(pos, staticParts[3], staticParts[4], out item4, out pos)) return false;
        if (!source.TryExtract(pos, staticParts[4], staticParts[5], out item5, out pos)) return false;
        if (!source.TryExtract(pos, staticParts[5], staticParts[6], out item6, out pos)) return false;
        if (!source.TryExtract(pos, staticParts[6], staticParts[7], out item7, out pos)) return false;
        return true;
    }

    /// <summary>
    /// Attempts to extract eight values from the source based on the pattern.
    /// </summary>
    /// <typeparam name="T1">The type of the first value to extract.</typeparam>
    /// <typeparam name="T2">The type of the second value to extract.</typeparam>
    /// <typeparam name="T3">The type of the third value to extract.</typeparam>
    /// <typeparam name="T4">The type of the fourth value to extract.</typeparam>
    /// <typeparam name="T5">The type of the fifth value to extract.</typeparam>
    /// <typeparam name="T6">The type of the sixth value to extract.</typeparam>
    /// <typeparam name="T7">The type of the seventh value to extract.</typeparam>
    /// <typeparam name="T8">The type of the eighth value to extract.</typeparam>
    /// <param name="source">The source text segment to extract from.</param>
    /// <param name="item1">The first extracted value, if successful.</param>
    /// <param name="item2">The second extracted value, if successful.</param>
    /// <param name="item3">The third extracted value, if successful.</param>
    /// <param name="item4">The fourth extracted value, if successful.</param>
    /// <param name="item5">The fifth extracted value, if successful.</param>
    /// <param name="item6">The sixth extracted value, if successful.</param>
    /// <param name="item7">The seventh extracted value, if successful.</param>
    /// <param name="item8">The eighth extracted value, if successful.</param>
    /// <returns>True if extraction succeeded; otherwise, false.</returns>
    public bool TryExtract<T1, T2, T3, T4, T5, T6, T7, T8>(TextSegment source, out T1 item1, out T2 item2, out T3 item3, out T4 item4, out T5 item5, out T6 item6, out T7 item7, out T8 item8)
        where T1 : IConvertible
        where T2 : IConvertible
        where T3 : IConvertible
        where T4 : IConvertible
        where T5 : IConvertible
        where T6 : IConvertible
        where T7 : IConvertible
        where T8 : IConvertible
    {
        item1 = default;
        item2 = default;
        item3 = default;
        item4 = default;
        item5 = default;
        item6 = default;
        item7 = default;
        item8 = default;
        int pos = 0;
        if (staticParts.Count <= 8) return false;
        if (!source.TryExtract(pos, staticParts[0], staticParts[1], out item1, out pos)) return false;
        if (!source.TryExtract(pos, staticParts[1], staticParts[2], out item2, out pos)) return false;
        if (!source.TryExtract(pos, staticParts[2], staticParts[3], out item3, out pos)) return false;
        if (!source.TryExtract(pos, staticParts[3], staticParts[4], out item4, out pos)) return false;
        if (!source.TryExtract(pos, staticParts[4], staticParts[5], out item5, out pos)) return false;
        if (!source.TryExtract(pos, staticParts[5], staticParts[6], out item6, out pos)) return false;
        if (!source.TryExtract(pos, staticParts[6], staticParts[7], out item7, out pos)) return false;
        if (!source.TryExtract(pos, staticParts[7], staticParts[8], out item8, out pos)) return false;
        return true;
    }
}
