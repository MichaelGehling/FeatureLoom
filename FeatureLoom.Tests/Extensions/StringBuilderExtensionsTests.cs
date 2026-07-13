using System;
using System.Globalization;
using System.Text;
using FeatureLoom.Collections;
using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using Xunit;

namespace FeatureLoom.Extensions
{
    public class StringBuilderExtensionsTests
    {
        [Fact]
        public void AppendInterpolatedAppendsLiteralsAndValues()
        {
            using var testContext = TestHelper.PrepareTestContext();

            string name = "World";
            int count = 3;
            var sb = new StringBuilder();
            sb.Append($"Hello {name}, you have {count} messages");

            Assert.Equal("Hello World, you have 3 messages", sb.ToString());
            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void AppendInterpolatedPreservesExistingContentAndReturnsSameInstance()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sb = new StringBuilder("start-");
            var returned = sb.Append($"{1}{2}{3}");

            Assert.Same(sb, returned);
            Assert.Equal("start-123", sb.ToString());
            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void AppendInterpolatedHandlesVariousPrimitiveTypes()
        {
            using var testContext = TestHelper.PrepareTestContext();

            byte b = 1;
            sbyte sb2 = -2;
            short s = -3;
            ushort us = 4;
            int i = -5;
            uint ui = 6;
            long l = -7;
            ulong ul = 8;
            bool flag = true;
            char c = 'X';

            // Routed through our handler (BuildString) rather than the built-in one used by sb.Append on net6+.
            string result = StringBuilder.BuildString($"{b},{sb2},{s},{us},{i},{ui},{l},{ul},{flag},{c}");

            Assert.Equal("1,-2,-3,4,-5,6,-7,8,True,X", result);
            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void AppendInterpolatedAppliesFormat()
        {
            using var testContext = TestHelper.PrepareTestContext();

            int value = 255;
            double d = 3.14159;
            var sb = new StringBuilder();
            sb.Append($"{value:X2}|{d.ToString("F2", CultureInfo.InvariantCulture)}");

            Assert.Equal("FF|3.14", sb.ToString());
            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void AppendInterpolatedAppliesAlignment()
        {
            using var testContext = TestHelper.PrepareTestContext();

            int value = 42;
            string result = StringBuilder.BuildString($"[{value,5}]-[{value,-5}]");

            Assert.Equal("[   42]-[42   ]", result);
            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void AppendInterpolatedHandlesNullValue()
        {
            using var testContext = TestHelper.PrepareTestContext();

            string nullString = null;
            var sb = new StringBuilder();
            sb.Append($"a{nullString}b");

            Assert.Equal("ab", sb.ToString());
            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void AppendInterpolatedHandlesTextSegment()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var segment = new TextSegment("abcXYZdef", 3, 3);
            string result = StringBuilder.BuildString($"<{segment}>");

            Assert.Equal("<XYZ>", result);
            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void AppendInterpolatedHandlesArraySegmentOfChar()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var chars = new char[] { 'a', 'b', 'c', 'd' };
            var segment = new ArraySegment<char>(chars, 1, 2);
            // Uses our custom handler via BuildString; the built-in sb.Append handler would call ArraySegment.ToString().
            string result = StringBuilder.BuildString($"<{segment}>");

            Assert.Equal("<bc>", result);
            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void BuildCachedStringProducesExpectedResult()
        {
            using var testContext = TestHelper.PrepareTestContext();

            string name = "World";
            int count = 3;
            string result = StringBuilder.BuildCachedString($"Hello {name}, you have {count} messages");

            Assert.Equal("Hello World, you have 3 messages", result);
            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void BuildCachedStringReturnsSharedInstanceForEqualContent()
        {
            using var testContext = TestHelper.PrepareTestContext();

            int id = 12345;
            string first = StringBuilder.BuildCachedString($"user:{id}");
            string second = StringBuilder.BuildCachedString($"user:{id}");

            Assert.Equal("user:12345", first);
            Assert.Equal(first, second);
            Assert.Same(first, second);
            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void BuildCachedStringUsesProvidedCache()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var cache = new StringInternCache(13, 128);
            int id = 999;
            string first = StringBuilder.BuildCachedString($"item:{id}", cache);
            string second = StringBuilder.BuildCachedString($"item:{id}", cache);

            Assert.Equal("item:999", first);
            Assert.Same(first, second);
            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void BuildStringProducesExpectedResult()
        {
            using var testContext = TestHelper.PrepareTestContext();

            string name = "World";
            int count = 3;
            string result = StringBuilder.BuildString($"Hello {name}, you have {count} messages");

            Assert.Equal("Hello World, you have 3 messages", result);
            Assert.False(TestHelper.HasAnyLogError());
        }
    }
}
