using System;
using System.Linq;
using FeatureLoom.Collections;
using Xunit;

namespace FeatureLoom.Collections
{
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
    }
}