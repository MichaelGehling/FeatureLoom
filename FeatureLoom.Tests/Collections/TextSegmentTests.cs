using System;
using System.Linq;
using FeatureLoom.Collections;
using Xunit;

namespace FeatureLoom.Collections;

public class TextSegmentTests
{
    [Fact]
    public void Constructor_FromString_Works()
    {
        var seg = new TextSegment("hello");
        Assert.Equal(5, seg.Count);
        Assert.Equal('h', seg[0]);
        Assert.Equal('o', seg[4]);
    }

    [Fact]
    public void Constructor_FromString_And_StartIndex_Works()
    {
        var seg = new TextSegment("hello", 2);
        Assert.Equal(3, seg.Count);
        Assert.Equal('l', seg[0]);
        Assert.Equal('o', seg[2]);
    }

    [Fact]
    public void Constructor_FromString_StartIndex_Length_Works()
    {
        var seg = new TextSegment("hello", 1, 3);
        Assert.Equal(3, seg.Count);
        Assert.Equal('e', seg[0]);
        Assert.Equal('l', seg[2]);
    }

    [Fact]
    public void Constructor_Throws_OnNull()
    {
        Assert.Throws<ArgumentNullException>(() => new TextSegment(null));
        Assert.Throws<ArgumentNullException>(() => new TextSegment(null, 0));
        Assert.Throws<ArgumentNullException>(() => new TextSegment(null, 0, 0));
    }

    [Fact]
    public void Constructor_Throws_OnOutOfBounds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextSegment("abc", -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextSegment("abc", 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextSegment("abc", 1, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextSegment("abc", -1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextSegment("abc", 2, -1));
    }

    [Fact]
    public void IsValid_And_IsEmptyOrInvalid_Work()
    {
        var empty = TextSegment.Empty;
        Assert.True(empty.IsValid);
        Assert.True(empty.IsEmptyOrInvalid);

        var seg = new TextSegment("a");
        Assert.True(seg.IsValid);
        Assert.False(seg.IsEmptyOrInvalid);
    }

    [Fact]
    public void SubSegment_Works()
    {
        var seg = new TextSegment("abcdef");
        var sub = seg.SubSegment(2, 3);
        Assert.Equal(3, sub.Count);
        Assert.Equal('c', sub[0]);
        Assert.Equal('e', sub[2]);
    }

    [Fact]
    public void TryFindIndex_Char_Works()
    {
        var seg = new TextSegment("abacad");
        Assert.True(seg.TryFindIndex('c', out int idx));
        Assert.Equal(3, idx);

        Assert.False(seg.TryFindIndex('z', out _));
    }

    [Fact]
    public void TryFindIndex_TextSegment_Works()
    {
        var seg = new TextSegment("ababcabc");
        var search = new TextSegment("abc");
        Assert.True(seg.TryFindIndex(search, out int idx));
        Assert.Equal(2, idx);

        var notFound = new TextSegment("xyz");
        Assert.False(seg.TryFindIndex(notFound, out _));
    }

    [Fact]
    public void TryFindIndex_WithStartIndex_Works()
    {
        var seg = new TextSegment("ababcabc");
        var search = new TextSegment("abc");
        Assert.True(seg.TryFindIndex(search, 3, out int idx));
        Assert.Equal(5, idx);

        Assert.False(seg.TryFindIndex(search, 6, out _));
    }

    [Fact]
    public void TryFindIndex_Char_WithStartIndex_Works()
    {
        var seg = new TextSegment("abacad");
        Assert.True(seg.TryFindIndex('a', 2, out int idx));
        Assert.Equal(2, idx);

        Assert.False(seg.TryFindIndex('z', 0, out _));
    }

    [Fact]
    public void TryFindLastIndex_TextSegment_Works()
    {
        var seg = new TextSegment("ababcabc");
        var search = new TextSegment("abc");
        Assert.True(seg.TryFindLastIndex(search, out int idx));
        Assert.Equal(5, idx);

        var notFound = new TextSegment("xyz");
        Assert.False(seg.TryFindLastIndex(notFound, out _));
    }

    [Fact]
    public void TryFindLastIndex_TextSegment_WithLastIndex_Works()
    {
        var seg = new TextSegment("ababcabcabc");
        var search = new TextSegment("abc");
        Assert.True(seg.TryFindLastIndex(search, 6, out int idx));
        Assert.Equal(2, idx);

        Assert.True(seg.TryFindLastIndex(search, 10, out int idx2));
        Assert.Equal(8, idx2);

        Assert.False(seg.TryFindLastIndex(search, 2, out _));
    }

    [Fact]
    public void TryFindLastIndex_Char_Works()
    {
        var seg = new TextSegment("abacad");
        Assert.True(seg.TryFindLastIndex('a', out int idx));
        Assert.Equal(4, idx);

        Assert.False(seg.TryFindLastIndex('z', out _));
    }

    [Fact]
    public void TryFindLastIndex_Char_WithLastIndex_Works()
    {
        var seg = new TextSegment("abacad");
        Assert.True(seg.TryFindLastIndex('a', 3, out int idx));
        Assert.Equal(2, idx);

        Assert.False(seg.TryFindLastIndex('z', 5, out _));
    }

    [Fact]
    public void Split_Works()
    {
        var seg = new TextSegment("a,b,,c");
        var parts = seg.Split(',').ToList();
        Assert.Equal(4, parts.Count);
        Assert.Equal("a", parts[0].ToString());
        Assert.Equal("b", parts[1].ToString());
        Assert.Equal("", parts[2].ToString());
        Assert.Equal("c", parts[3].ToString());
    }

    [Fact]
    public void Split_SkipEmpty_Works()
    {
        var seg = new TextSegment("a,b,,c");
        var parts = seg.Split(',', skipEmpty: true).ToList();
        Assert.Equal(3, parts.Count);
        Assert.Equal("a", parts[0].ToString());
        Assert.Equal("b", parts[1].ToString());
        Assert.Equal("c", parts[2].ToString());
    }

    [Fact]
    public void Trim_Works()
    {
        var seg = new TextSegment("  hello  ");
        var trimmed = seg.Trim();
        Assert.Equal("hello", trimmed.ToString());
    }

    [Fact]
    public void Trim_WithCharArray_Works()
    {
        var seg = new TextSegment(".,,hello,,");
        var trimmed = seg.Trim(',', '.');
        Assert.Equal("hello", trimmed.ToString());
    }

    [Fact]
    public void Trim_WithChar_Works()
    {
        var seg = new TextSegment("xxhelloxx");
        var trimmed = seg.Trim('x');
        Assert.Equal("hello", trimmed.ToString());
    }

    [Fact]
    public void TrimStart_Works()
    {
        var seg = new TextSegment("  hello  ");
        var trimmed = seg.TrimStart();
        Assert.Equal("hello  ", trimmed.ToString());
    }

    [Fact]
    public void TrimEnd_Works()
    {
        var seg = new TextSegment("  hello  ");
        var trimmed = seg.TrimEnd();
        Assert.Equal("  hello", trimmed.ToString());
    }

    [Fact]
    public void TrimStart_WithCharArray_Works()
    {
        var seg = new TextSegment(".,,hello,,");
        var trimmed = seg.TrimStart(',', '.');
        Assert.Equal("hello,,", trimmed.ToString());
    }

    [Fact]
    public void TrimEnd_WithCharArray_Works()
    {
        var seg = new TextSegment(".,,hello,,");
        var trimmed = seg.TrimEnd(',', '.');
        Assert.Equal(".,,hello", trimmed.ToString());
    }

    [Fact]
    public void TrimStart_WithChar_Works()
    {
        var seg = new TextSegment("xxhelloxx");
        var trimmed = seg.TrimStart('x');
        Assert.Equal("helloxx", trimmed.ToString());
    }

    [Fact]
    public void TrimEnd_WithChar_Works()
    {
        var seg = new TextSegment("xxhelloxx");
        var trimmed = seg.TrimEnd('x');
        Assert.Equal("xxhello", trimmed.ToString());
    }

    [Fact]
    public void StartsWith_Char_Works()
    {
        var seg = new TextSegment("abc");
        Assert.True(seg.StartsWith('a'));
        Assert.False(seg.StartsWith('b'));
    }

    [Fact]
    public void EndsWith_Char_Works()
    {
        var seg = new TextSegment("abc");
        Assert.True(seg.EndsWith('c'));
        Assert.False(seg.EndsWith('b'));
    }

    [Fact]
    public void Contains_Char_Works()
    {
        var seg = new TextSegment("abc");
        Assert.True(seg.Contains('b'));
        Assert.False(seg.Contains('z'));
    }

    [Fact]
    public void Equality_Works()
    {
        var seg1 = new TextSegment("hello");
        var seg2 = new TextSegment("hello");
        var seg3 = new TextSegment("hell");
        Assert.True(seg1.Equals(seg2));
        Assert.True(seg1 == seg2);
        Assert.False(seg1 != seg2);
        Assert.False(seg1.Equals(seg3));
    }

    [Fact]
    public void Equals_String_Works()
    {
        var seg = new TextSegment("test");
        Assert.True(seg.Equals("test"));
        Assert.False(seg.Equals("other"));
    }

    [Fact]
    public void ToString_ReturnsExpected()
    {
        var seg = new TextSegment("abcdef", 2, 3);
        Assert.Equal("cde", seg.ToString());
    }

#if !NETSTANDARD2_0
    [Fact]
    public void AsSpan_Works()
    {
        var seg = new TextSegment("abcdef", 2, 3);
        var span = seg.AsSpan();
        Assert.Equal("cde", span.ToString());
    }
#endif

    [Fact]
    public void ImplicitConversions_Work()
    {
        TextSegment seg = "abc";
        string str = seg;
        Assert.Equal("abc", str);
    }

    [Theory]
    [InlineData("true", typeof(bool), true)]
    [InlineData("123", typeof(byte), (byte)123)]
    [InlineData("A", typeof(char), 'A')]
    [InlineData("2024-06-19", typeof(DateTime), "2024-06-19")] // DateTime parsed below
    [InlineData("123.45", typeof(decimal), 123.45)]
    [InlineData("123.45", typeof(double), 123.45)]
    [InlineData("12345", typeof(short), (short)12345)]
    [InlineData("123456", typeof(int), 123456)]
    [InlineData("1234567890123", typeof(long), 1234567890123L)]
    [InlineData("42", typeof(sbyte), (sbyte)42)]
    [InlineData("3.14", typeof(float), 3.14f)]
    [InlineData("54321", typeof(ushort), (ushort)54321)]
    [InlineData("1234567890", typeof(uint), 1234567890u)]
    [InlineData("1234567890123456789", typeof(ulong), 1234567890123456789ul)]
    [InlineData("test", typeof(string), "test")]
    public void ToType_ConvertsToSupportedTypes(string input, Type targetType, object expected)
    {
        var seg = new TextSegment(input);

        object result = seg.ToType(targetType, System.Globalization.CultureInfo.InvariantCulture);

        if (targetType == typeof(DateTime))
        {
            // DateTime needs to be parsed for expected value
            var expectedDate = DateTime.Parse((string)expected, System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(expectedDate, result);
        }
        else if (targetType == typeof(float))
        {
            Assert.Equal((float)expected, (float)result, 3);
        }
        else if (targetType == typeof(double))
        {
            Assert.Equal((double)expected, (double)result, 6);
        }
        else if (targetType == typeof(decimal))
        {
            Assert.Equal(Convert.ToDecimal(expected), Convert.ToDecimal(result));
        }
        else
        {
            Assert.Equal(expected, result);
        }
    }

    [Fact]
    public void ToType_ReturnsSelfForTextSegment()
    {
        var seg = new TextSegment("abc");
        var result = seg.ToType(typeof(TextSegment), null);
        Assert.IsType<TextSegment>(result);
        Assert.Equal(seg, (TextSegment)result);
    }

    [Fact]
    public void ToType_ThrowsOnNullType()
    {
        var seg = new TextSegment("abc");
        Assert.Throws<ArgumentNullException>(() => seg.ToType(null, null));
    }

    [Fact]
    public void ToType_FallbacksToChangeTypeForUnknownType()
    {
        var seg = new TextSegment("abc");
        // object is not handled explicitly, so this will fallback to Convert.ChangeType
        var result = seg.ToType(typeof(object), null);
        Assert.Equal("abc", result);
    }

    [Fact]
    public void ToType_NullOnInvalidConversion()
    {
        var seg = new TextSegment("notanumber");
        Assert.Null(seg.ToType(typeof(int), null));
    }

    [Fact]
    public void TrimStart_String_Works()
    {
        var seg = new TextSegment("foobarfoobarhellofoobar");
        var trimmed = seg.TrimStart("foobar");
        Assert.Equal("hellofoobar", trimmed.ToString());

        // Should trim multiple occurrences
        var seg2 = new TextSegment("abcabcabcxyz");
        var trimmed2 = seg2.TrimStart("abc");
        Assert.Equal("xyz", trimmed2.ToString());

        // Should not trim if not at start
        var seg3 = new TextSegment("xabcabc");
        var trimmed3 = seg3.TrimStart("abc");
        Assert.Equal("xabcabc", trimmed3.ToString());

        // Should return empty if all trimmed
        var seg4 = new TextSegment("abcabc");
        var trimmed4 = seg4.TrimStart("abc");
        Assert.Equal("", trimmed4.ToString());

        // Should return original if trimStr is empty
        var seg5 = new TextSegment("abcabc");
        var trimmed5 = seg5.TrimStart("");
        Assert.Equal("abcabc", trimmed5.ToString());

        // Should return original if trimStr is null
        var seg6 = new TextSegment("abcabc");
        var trimmed6 = seg6.TrimStart((string)null);
        Assert.Equal("abcabc", trimmed6.ToString());
    }

    [Fact]
    public void TrimEnd_String_Works()
    {
        var seg = new TextSegment("foobarhellofoobarfoobar");
        var trimmed = seg.TrimEnd("foobar");
        Assert.Equal("foobarhello", trimmed.ToString());

        // Should trim multiple occurrences
        var seg2 = new TextSegment("xyzabcabcabc");
        var trimmed2 = seg2.TrimEnd("abc");
        Assert.Equal("xyz", trimmed2.ToString());

        // Should not trim if not at end
        var seg3 = new TextSegment("abcabcx");
        var trimmed3 = seg3.TrimEnd("abc");
        Assert.Equal("abcabcx", trimmed3.ToString());

        // Should return empty if all trimmed
        var seg4 = new TextSegment("abcabc");
        var trimmed4 = seg4.TrimEnd("abc");
        Assert.Equal("", trimmed4.ToString());

        // Should return original if trimStr is empty
        var seg5 = new TextSegment("abcabc");
        var trimmed5 = seg5.TrimEnd("");
        Assert.Equal("abcabc", trimmed5.ToString());

        // Should return original if trimStr is null
        var seg6 = new TextSegment("abcabc");
        var trimmed6 = seg6.TrimEnd((string)null);
        Assert.Equal("abcabc", trimmed6.ToString());
    }

    [Fact]
    public void SubSegment_BasicExtraction_Works()
    {
        var seg = new TextSegment("abc[START]middle[END]xyz");
        int rest;
        var result = seg.SubSegment(0, "[START]", "[END]", out rest);
        Assert.NotNull(result);
        Assert.Equal("middle", result.Value.ToString());
        Assert.Equal(seg.ToString().IndexOf("[END]"), rest);
    }

    [Fact]
    public void SubSegment_IncludeSearchStrings_Works()
    {
        var seg = new TextSegment("abc[START]middle[END]xyz");
        int rest;
        var result = seg.SubSegment(0, "[START]", "[END]", out rest, includeSearchStrings: true);
        Assert.NotNull(result);
        Assert.Equal("[START]middle[END]", result.Value.ToString());
    }

    [Fact]
    public void SubSegment_EmptyStartAfter_Works()
    {
        var seg = new TextSegment("foo:bar;baz");
        int rest;
        var result = seg.SubSegment(0, TextSegment.Empty, ";", out rest);
        Assert.NotNull(result);
        Assert.Equal("foo:bar", result.Value.ToString());
    }

    [Fact]
    public void SubSegment_EmptyEndBefore_Works()
    {
        var seg = new TextSegment("foo:bar;baz");
        int rest;
        var result = seg.SubSegment(0, ";", TextSegment.Empty, out rest);
        Assert.NotNull(result);
        Assert.Equal("baz", result.Value.ToString());
    }

    [Fact]
    public void SubSegment_NotFound_ReturnsNull()
    {
        var seg = new TextSegment("foo:bar;baz");
        int rest;
        var result = seg.SubSegment(0, "[", "]", out rest);
        Assert.Null(result);
    }

    [Fact]
    public void SubSegment_StartAfterNotFound_ReturnsNull()
    {
        var seg = new TextSegment("foo:bar;baz");
        int rest;
        var result = seg.SubSegment(0, "NOPE", ";", out rest);
        Assert.Null(result);
    }

    [Fact]
    public void SubSegment_EndBeforeNotFound_ReturnsNull()
    {
        var seg = new TextSegment("foo:bar;baz");
        int rest;
        var result = seg.SubSegment(0, "foo:", "NOPE", out rest);
        Assert.Null(result);
    }

    [Fact]
    public void SubSegment_Handles_OverlappingMarkers()
    {
        var seg = new TextSegment("xx[START][END]yy");
        int rest;
        var result = seg.SubSegment(0, "[START]", "[END]", out rest);
        Assert.NotNull(result);
        Assert.Equal("", result.Value.ToString());
    }

    [Fact]
    public void SubSegment_Handles_NestedMarkers()
    {
        var seg = new TextSegment("a[START]b[START]c[END]d[END]e");
        int rest;
        var result = seg.SubSegment(0, "[START]", "[END]", out rest);
        Assert.NotNull(result);
        Assert.Equal("b[START]c", result.Value.ToString());
    }
}