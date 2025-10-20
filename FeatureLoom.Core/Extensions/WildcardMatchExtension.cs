using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;
using FeatureLoom.Collections;

namespace FeatureLoom.Extensions
{
    /// <summary>
    /// Wildcard pattern matching for strings and TextSegment without allocations.
    /// Supported wildcards:
    /// - '?' matches exactly one character
    /// - '*' matches zero or more characters
    /// Notes:
    /// - No escaping; '*' and '?' are always treated as wildcards.
    /// - Case-sensitivity is controlled via <see cref="StringComparison"/>.
    /// - Uses TextSegment/segment views to avoid substring allocations.
    /// </summary>
    public static class WildcardMatchExtension
    {
        /// <summary>
        /// Matches a text segment against a wildcard pattern segment using the provided comparison.
        /// </summary>
        /// <param name="text">The text to test.</param>
        /// <param name="pattern">The pattern containing literals and '*'/'?'.</param>
        /// <param name="comparison">String comparison (use Ordinal or OrdinalIgnoreCase).</param>
        /// <returns>True if text matches pattern; otherwise false.</returns>
        /// <remarks>
        /// Fast-paths:
        /// - If pattern is exactly "*", returns true.
        /// Algorithm outline:
        /// - Iteratively consume literal prefixes (until next wildcard).
        /// - Handle '?' by consuming one char.
        /// - Handle '*' by collapsing consecutive wildcards and consuming mandatory '?' after it.
        ///   If there is no next literal, '*' consumes the rest and returns true.
        ///   Otherwise, backtrack by trying each occurrence of the next literal (single-char and multi-char paths).
        /// </remarks>
        public static bool MatchesWildcardPattern(this TextSegment text, TextSegment pattern, StringComparison comparison)
        {
            if (!text.IsValid) return false;
            if (!pattern.IsValid) return false;
            if (pattern.Equals("*")) return true;

            var remainingText = new VariableTextSection(text);
            var remainingPattern = new VariableTextSection(pattern);         

            return MatchCore(remainingText, remainingPattern, comparison);
        }

        /// <summary>
        /// Core matching loop. Consumes literal prefixes, then resolves wildcards.
        /// For '*', collapses adjacent wildcards, consumes following '?' greedily (by advancing text),
        /// then either:
        /// - returns true if '*' is trailing, or
        /// - tries each candidate position where the next literal occurs (char-optimized and multi-char paths).
        /// </summary>
        private static bool MatchCore(VariableTextSection remainingText, VariableTextSection remainingPattern, StringComparison comparison)
        {
            while (remainingPattern.length > 0)
            {
                // Consume and verify literal prefix (until next '?' or '*')
                VariableTextSection sectionWithoutWildcard = remainingPattern.SectionUntilNextWildcard();
                if (sectionWithoutWildcard.length > 0)
                {
                    if (!remainingText.StartsWith(sectionWithoutWildcard, comparison)) return false;
                    remainingPattern.SkipChars(sectionWithoutWildcard.length);
                    remainingText.SkipChars(sectionWithoutWildcard.length);
                }

                // Pattern exhausted => must have consumed whole text
                if (remainingPattern.length == 0) return remainingText.length == 0;

                // Resolve next wildcard
                char wildCard = remainingPattern[0];
                remainingPattern.SkipChars(1);
                if (wildCard == '?')
                {
                    // '?' consumes exactly one char
                    if (remainingText.length == 0) return false;
                    remainingText.SkipChars(1);
                }
                else if (wildCard == '*')
                {
                    // Collapse subsequent wildcards and consume mandatory '?' directly on the text
                    sectionWithoutWildcard = remainingPattern.SectionUntilNextWildcard();
                    while (sectionWithoutWildcard.length == 0 && remainingPattern.length > 0)
                    {
                        wildCard = remainingPattern[0];
                        remainingPattern.SkipChars(1);
                        if (wildCard == '?')
                        {
                            if (remainingText.length == 0) return false;
                            remainingText.SkipChars(1);
                        }
                        sectionWithoutWildcard = remainingPattern.SectionUntilNextWildcard();
                    }

                    // No more literal after '*' => '*' matches the rest (we already consumed required '?' above)
                    if (sectionWithoutWildcard.length == 0) return true; 
                    
                    if (sectionWithoutWildcard.length == 1)
                    {
                        // Single-char next literal: use char search and slide by pos+1 for overlaps
                        char needle = sectionWithoutWildcard[0];
                        // Advance pattern by that 1-char literal once; reuse the remainingPattern for all attempts
                        remainingPattern.SkipChars(1);
                        while (remainingText.TryFindIndex(needle, comparison, out var pos))
                        {
                            // Consume up to and including the matched char, then recurse for the remainder
                            remainingText.SkipChars(pos + 1);
                            if (MatchCore(remainingText, remainingPattern, comparison)) return true;
                            // If recursion failed, loop continues searching from the new position
                        }
                    }
                    else
                    {
                        // Multi-char next literal: find each occurrence, consume literal, recurse; then slide by index+1 for overlaps
                        remainingPattern.SkipChars(sectionWithoutWildcard.length);
                        while (remainingText.TryFindIndex(sectionWithoutWildcard, comparison, out int index))
                        {
                            var nextText = remainingText.SubSection(index + sectionWithoutWildcard.length);
                            if (MatchCore(nextText, remainingPattern, comparison)) return true;
                            // Slide start by one to allow overlapping matches, then continue search
                            remainingText.SkipChars(index + 1);
                        }
                    }
                    
                    // Exhausted candidates after '*' without success
                    return false;
                }
            }
            // Pattern done: text must be fully consumed
            return remainingText.length == 0;
        }

        /// <summary>
        /// Lightweight, non-allocating view into a substring of a string, with helper operations
        /// tailored for wildcard matching. All indices are relative to the view.
        /// </summary>
        private struct VariableTextSection
        {
            public static readonly VariableTextSection Empty = new VariableTextSection(""));

            string text;
            int startIndex;
            public int length;

            /// <summary>
            /// Advances the view by <paramref name="numChars"/> characters.
            /// If over-advanced, the view becomes empty.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SkipChars(int numChars)
            {
                startIndex += numChars;
                length -= numChars;
                if (length < 0)
                {
                    text = "";
                    startIndex = 0;
                    length = 0;
                }
            }

            public VariableTextSection(TextSegment text) : this()
            {
                this.text = text.UnderlyingString;
                this.startIndex = text.Offset;
                this.length = text.Count;
            }

            private VariableTextSection(string text, int startIndex) : this()
            {
                if (startIndex >= text.Length)
                {
                    this.text = "";
                    return;
                }

                this.text = text;
                this.startIndex = startIndex;
                this.length = text.Length - startIndex;
            }

            private VariableTextSection(string text, int startIndex, int length) : this()
            {
                if (startIndex + length > text.Length)
                {
                    this.text = "";
                    return;
                }

                this.text = text;
                this.startIndex = startIndex;
                this.length = length;
            }

            public override string ToString() => length == 0 ? "" : text.Substring(startIndex, length);

            public char this[int index] => text[startIndex + index];

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public VariableTextSection SubSection(int startIndex) => new VariableTextSection(text, this.startIndex + startIndex);
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public VariableTextSection SubSection(int startIndex, int length) => new VariableTextSection(text, this.startIndex + startIndex, length);

            /// <summary>
            /// Checks whether this view starts with another view, using the given comparison.
            /// Bound-checked: returns false if other is longer than this.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool StartsWith(VariableTextSection other, StringComparison comparison)
            {
                if (other.length > this.length) return false;
                return string.Compare(this.text, this.startIndex, other.text, other.startIndex, other.length, comparison) == 0;
            }

            /// <summary>
            /// Finds the first occurrence of another view inside this view (relative index).
            /// Uses the given comparison; intended for Ordinal/OrdinalIgnoreCase.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryFindIndex(VariableTextSection other, StringComparison comparison, out int index)
            {
                int lastStart = this.length - other.length;
                for (index = 0; index <= lastStart; index++)
                {
                    if (string.Compare(this.text, this.startIndex + index, other.text, other.startIndex, other.length, comparison) == 0)
                        return true;
                }
                index = -1;
                return false;
            }

            /// <summary>
            /// Finds the first occurrence of a character inside this view (relative index).
            /// Uses Ordinal or OrdinalIgnoreCase semantics.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryFindIndex(char target, StringComparison comparison, out int index)
            {
                // Only Ordinal/OrdinalIgnoreCase are passed in current code
                bool ignoreCase = comparison == StringComparison.OrdinalIgnoreCase;
                if (ignoreCase)
                {
                    target = char.ToUpperInvariant(target);
                    for (index = 0; index < this.length; index++)
                    {
                        if (char.ToUpperInvariant(text[startIndex + index]) == target) return true;
                    }
                }
                else
                {
                    for (index = 0; index < this.length; index++)
                    {
                        if (text[startIndex + index] == target) return true;
                    }
                }
                index = -1;
                return false;
            }

            /// <summary>
            /// Returns the literal section before the next wildcard ('?' or '*'), or the whole view if none.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public VariableTextSection SectionUntilNextWildcard()
            {
                for (int i = 0; i < this.length; i++)
                {
                    char c = this[i];
                    if (c == '?' || c == '*') return this.SubSection(0, i);
                }
                return this;
            }
        }

        /// <summary>
        /// Matches a text segment against a wildcard pattern segment using the provided comparison.
        /// </summary>
        /// <param name="text">The text to test.</param>
        /// <param name="pattern">The pattern containing literals and '*'/'?'.</param>
        /// <returns>True if text matches pattern; otherwise false.</returns>
        /// <remarks>
        /// Fast-paths:
        /// - If pattern is exactly "*", returns true.
        /// Algorithm outline:
        /// - Iteratively consume literal prefixes (until next wildcard).
        /// - Handle '?' by consuming one char.
        /// - Handle '*' by collapsing consecutive wildcards and consuming mandatory '?' after it.
        ///   If there is no next literal, '*' consumes the rest and returns true.
        ///   Otherwise, backtrack by trying each occurrence of the next literal (single-char and multi-char paths).
        /// </remarks>
        public static bool MatchesWildcardPattern(this TextSegment text, TextSegment pattern) 
            => MatchesWildcardPattern(text, pattern, StringComparison.Ordinal);

        /// <summary>
        /// Matches a string against a wildcard pattern string using the provided comparison.
        /// </summary>
        /// <param name="text">The text to test.</param>
        /// <param name="pattern">The pattern containing literals and '*'/'?'.</param>
        /// <returns>True if text matches pattern; otherwise false.</returns>
        /// <remarks>
        /// Fast-paths:
        /// - If pattern is exactly "*", returns true.
        /// Algorithm outline:
        /// - Iteratively consume literal prefixes (until next wildcard).
        /// - Handle '?' by consuming one char.
        /// - Handle '*' by collapsing consecutive wildcards and consuming mandatory '?' after it.
        ///   If there is no next literal, '*' consumes the rest and returns true.
        ///   Otherwise, backtrack by trying each occurrence of the next literal (single-char and multi-char paths).
        /// </remarks>
        public static bool MatchesWildcardPattern(this string text, string pattern)
        {
            if (text == null || pattern == null) return false;
            if (pattern == "*") return true;

            return MatchesWildcardPattern(new TextSegment(text), new TextSegment(pattern), StringComparison.Ordinal);
        }

        /// <summary>
        /// Matches a string against a wildcard pattern string using the provided comparison.
        /// </summary>
        /// <param name="text">The text to test.</param>
        /// <param name="pattern">The pattern containing literals and '*'/'?'.</param>
        /// <param name="ignoreCase">True means comparison uses OrdinalIgnoreCase.</param>
        /// <returns>True if text matches pattern; otherwise false.</returns>
        /// <remarks>
        /// Fast-paths:
        /// - If pattern is exactly "*", returns true.
        /// Algorithm outline:
        /// - Iteratively consume literal prefixes (until next wildcard).
        /// - Handle '?' by consuming one char.
        /// - Handle '*' by collapsing consecutive wildcards and consuming mandatory '?' after it.
        ///   If there is no next literal, '*' consumes the rest and returns true.
        ///   Otherwise, backtrack by trying each occurrence of the next literal (single-char and multi-char paths).
        /// </remarks>
        public static bool MatchesWildcardPattern(this string text, string pattern, bool ignoreCase)
        {
            if (text == null || pattern == null) return false;
            if (pattern == "*") return true;

            var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return MatchesWildcardPattern(new TextSegment(text), new TextSegment(pattern), comparison);
        }
    }
}