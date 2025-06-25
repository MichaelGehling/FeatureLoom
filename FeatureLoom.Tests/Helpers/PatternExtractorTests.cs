using System;
using FeatureLoom.Collections;
using FeatureLoom.Helpers;
using Xunit;

namespace FeatureLoom.Helpers;

public class PatternExtractorTests
{
    [Fact]
    public void Extracts_Single_Value()
    {
        var pattern = "Hello {name}!";
        var extractor = new PatternExtractor(pattern);

        Assert.True(extractor.TryExtract("Hello John!", out string name));
        Assert.Equal("John", name);
    }

    [Fact]
    public void Extracts_Two_Values()
    {
        var pattern = new TextSegment("({x},{y})");
        var extractor = new PatternExtractor(pattern);

        Assert.True(extractor.TryExtract(new TextSegment("(12,34)"), out int x, out int y));
        Assert.Equal(12, x);
        Assert.Equal(34, y);
    }

    [Fact]
    public void Extracts_Three_Values()
    {
        var pattern = new TextSegment("{a}-{b}-{c}");
        var extractor = new PatternExtractor(pattern);

        Assert.True(extractor.TryExtract(new TextSegment("foo-bar-baz"), out string a, out string b, out string c));
        Assert.Equal("foo", a);
        Assert.Equal("bar", b);
        Assert.Equal("baz", c);
    }

    [Fact]
    public void Returns_False_On_No_Match()
    {
        var pattern = new TextSegment("Hello {name}!");
        var extractor = new PatternExtractor(pattern);

        Assert.False(extractor.TryExtract(new TextSegment("Hi John!"), out string name));
    }

    [Fact]
    public void Throws_On_Consecutive_Placeholders()
    {
        var pattern = new TextSegment("foo{}{}bar");
        Assert.Throws<Exception>(() => new PatternExtractor(pattern));
    }

    [Fact]
    public void Extracts_With_Empty_Static_At_End()
    {
        var pattern = new TextSegment("foo{val}");
        var extractor = new PatternExtractor(pattern);

        Assert.True(extractor.TryExtract(new TextSegment("foo123"), out int val));
        Assert.Equal(123, val);
    }

    [Fact]
    public void Extracts_With_Empty_Static_At_Start()
    {
        var pattern = new TextSegment("{val}bar");
        var extractor = new PatternExtractor(pattern);

        Assert.True(extractor.TryExtract(new TextSegment("123bar"), out int val));
        Assert.Equal(123, val);
    }

    [Fact]
    public void Extracts_Multiple_Types()
    {
        var pattern = new TextSegment("A:{a},B:{b},C:{c}");
        var extractor = new PatternExtractor(pattern);

        Assert.True(extractor.TryExtract(new TextSegment("A:1,B:2.5,C:foo"), out int a, out double b, out string c));
        Assert.Equal(1, a);
        Assert.Equal(2.5, b, 3);
        Assert.Equal("foo", c);
    }

    [Fact]
    public void PatternExtractor_With_FirstStaticElement()
    {
        var pattern = new TextSegment("foo{bar}baz");
        var extractor = new PatternExtractor(pattern, out TextSegment first, false);

        Assert.Equal("foo", first.ToString());
        Assert.True(extractor.TryExtract(new TextSegment("foo123baz"), out int bar));
        Assert.Equal(123, bar);
    }

    [Fact]
    public void PatternExtractor_With_RemoveFirstStaticElement()
    {
        var pattern = new TextSegment("foo{bar}baz");
        var extractor = new PatternExtractor(pattern, out TextSegment first, true);

        Assert.Equal("foo", first.ToString());
        // After removal, first static part is empty, so it should still match
        Assert.True(extractor.TryExtract(new TextSegment("123baz"), out int bar));
        Assert.Equal(123, bar);
    }
}