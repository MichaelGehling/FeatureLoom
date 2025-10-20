using System;
using FeatureLoom.Collections;
using FeatureLoom.Extensions;
using Xunit;

namespace FeatureLoom.Extensions;

public class WildcardMatchExtensionTests
{
    [Theory]
    // Exact match / empties
    [InlineData("", "", true)]
    [InlineData("", "a", false)]
    [InlineData("abc", "abc", true)]
    [InlineData("abc", "ab", false)]
    // Single '?'
    [InlineData("a", "?", true)]
    [InlineData("ab", "?", false)]
    [InlineData("ab", "??", true)]
    [InlineData("abc", "??", false)]
    // Single '*'
    [InlineData("", "*", true)]
    [InlineData("abc", "*", true)]
    [InlineData("abc", "a*", true)]
    [InlineData("abc", "*c", true)]
    [InlineData("abc", "a*c", true)]
    [InlineData("abcd", "a*d", true)]
    [InlineData("abcd", "a*e", false)]
    [InlineData("aba", "*a", true)]
    [InlineData("aba1", "*a?", true)]
    // Multiple '*' compress
    [InlineData("abc", "a**c", true)]
    // Mixed patterns
    [InlineData("abc", "a*b*c", true)]
    [InlineData("axxxbc", "a*b*c", true)]
    [InlineData("ac", "a*b*c", false)]
    [InlineData("aa", "*a", true)]
    // Filename-like
    [InlineData("foo.txt", "*.txt", true)]
    [InlineData("foo.txt", "*.log", false)]
    // Suffix '?' with '*'
    [InlineData("ab", "*?", true)]
    [InlineData("a", "*??", false)]
    [InlineData("abc", "*??", true)]
    public void MatchesWildcard_basic_and_edges(string text, string pattern, bool expected)
    {
        Assert.Equal(expected, text.MatchesWildcardPattern(pattern));
    }

    [Theory]
    [InlineData("ababa", "*??ba", true)]
    [InlineData("baba", "*??ba", true)]
    [InlineData("xxba", "*??ba", true)]
    public void MatchesWildcard_star_with_min_random_charcount(string text, string pattern, bool expected)
    {
        Assert.Equal(expected, text.MatchesWildcardPattern(pattern));
    }

    [Fact]
    public void MatchesWildcard_handles_nulls_gracefully()
    {
        string s = null;
        Assert.False(s.MatchesWildcardPattern(null));
        Assert.False(s.MatchesWildcardPattern("*"));
        Assert.False("".MatchesWildcardPattern(null));
    }

    [Theory]
    [InlineData("FILE", "file", true)]
    [InlineData("file", "F*E", true)]
    [InlineData("FiLeNaMe.TxT", "*.tXt", true)]
    [InlineData("FILE", "file", false)] // same inputs, but ignoreCase = false => should be false
    public void MatchesWildcard_ignore_case_overload(string text, string pattern, bool expected)
    {
        if (expected) Assert.True(text.MatchesWildcardPattern(pattern, ignoreCase: true));            
        else Assert.False(text.MatchesWildcardPattern(pattern, ignoreCase: false));            
    }

    [Fact]
    public void MatchesWildcard_TextSegment_with_partial_string()
    {
        TextSegment pattern = new TextSegment("x*y", 1);
        TextSegment text = new TextSegment("123yz", 0, 4);
        Assert.True(text.MatchesWildcardPattern(pattern));
    }

    [Theory]
    [InlineData("aaaa", "*aa*a", true)]   // overlapping multi-char literal
    [InlineData("ababa", "*aba", true)]   // overlapping suffix literal
    [InlineData("ababa", "*bab", false)]   // middle overlap, wrong ending
    [InlineData("ababa", "*bab?", true)]   // middle overlap
    [InlineData("ababa", "*aba?", false)] // needs one more char
    public void MatchesWildcard_overlapping_cases(string text, string pattern, bool expected)
    {
        Assert.Equal(expected, text.MatchesWildcardPattern(pattern));
    }
}