using FeatureLoom.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using FeatureLoom.UndoRedo;
using FeatureLoom.Helpers;

namespace FeatureLoom.Extensions
{
    
    public class StringExtensionsTests
    {

        [Fact]
        public void TrimStartCanRemoveMultipleFragments()
        {
            using var testContext = TestHelper.PrepareTestContext();

            Assert.Equal("XyzAbc", "XyzAbc".TrimStart("Abc"));
            Assert.Equal("Xyz", "AbcXyz".TrimStart("Abc"));
            Assert.Equal("Xyz", "AbcAbcXyz".TrimStart("Abc"));

            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void TrimEndCanRemoveMultipleFragments()
        {
            using var testContext = TestHelper.PrepareTestContext();

            Assert.Equal("XyzAbc", "XyzAbc".TrimEnd("Xyz"));
            Assert.Equal("Abc", "AbcXyz".TrimEnd("Xyz"));
            Assert.Equal("Abc", "AbcXyzXyz".TrimEnd("Xyz"));

            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void TrimStartCanRemoveWholeString()
        {
            using var testContext = TestHelper.PrepareTestContext();

            Assert.Equal("", "Abc".TrimStart("Abc"));            

            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void TrimEndCanRemoveWholeString()
        {
            using var testContext = TestHelper.PrepareTestContext();

            Assert.Equal("", "Abc".TrimEnd("Abc"));

            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void BuildWithCache_NullBuilder_ReturnsNull()
        {
            using var testContext = TestHelper.PrepareTestContext();

            StringBuilder sb = null;
            Assert.Null(sb.BuildWithCache());

            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void BuildWithCache_EmptyBuilder_ReturnsStringEmpty()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var sb = new StringBuilder();
            Assert.Same(string.Empty, sb.BuildWithCache());

            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void BuildWithCache_ReturnsContentValue()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var cache = new Collections.StringInternCache(8, 1024);
            var sb = new StringBuilder().Append("Hello").Append(' ').Append("World");

            Assert.Equal("Hello World", sb.BuildWithCache(cache));

            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void BuildWithCache_ReturnsSameInstance_ForEqualContent()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var cache = new Collections.StringInternCache(8, 1024);

            string first = new StringBuilder().Append("abc").Append(123).BuildWithCache(cache);
            string second = new StringBuilder().Append("abc").Append(123).BuildWithCache(cache);

            Assert.Equal("abc123", first);
            Assert.Same(first, second);

            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void BuildWithCache_UsesSharedCache_WhenCacheIsNull()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var previous = Collections.StringInternCache.Shared;
            try
            {
                Collections.StringInternCache.Shared = new Collections.StringInternCache(8, 1024);

                string viaBuilder = new StringBuilder().Append("shared_content").BuildWithCache();
                string viaShared = Collections.StringInternCache.Shared.Intern("shared_content");

                Assert.Same(viaBuilder, viaShared);
            }
            finally
            {
                Collections.StringInternCache.Shared = previous;
            }

            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void BuildWithCache_HandlesContentLongerThanStackLimit()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var cache = new Collections.StringInternCache(8, 4096);
            string longContent = new string('x', 1000);

            string first = new StringBuilder(longContent).BuildWithCache(cache);
            string second = new StringBuilder(longContent).BuildWithCache(cache);

            Assert.Equal(longContent, first);
            Assert.Same(first, second);

            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void Intern_Null_ReturnsNull()
        {
            using var testContext = TestHelper.PrepareTestContext();

            string str = null;
            Assert.Null(str.Intern());

            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void Intern_ReturnsSameInstance_ForEqualContent()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var cache = new Collections.StringInternCache(8, 1024);

            string a = new string("dedup".ToCharArray());
            string b = new string("dedup".ToCharArray());
            Assert.NotSame(a, b);

            string first = a.Intern(cache);
            string second = b.Intern(cache);

            Assert.Same(first, second);
            Assert.Same(a, second); // first deduplicated instance wins

            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void Intern_UsesSharedCache_WhenCacheIsNull()
        {
            using var testContext = TestHelper.PrepareTestContext();

            var previous = Collections.StringInternCache.Shared;
            try
            {
                Collections.StringInternCache.Shared = new Collections.StringInternCache(8, 1024);

                string viaExtension = new string("shared_str".ToCharArray()).Intern();
                string viaShared = Collections.StringInternCache.Shared.Intern("shared_str");

                Assert.Same(viaExtension, viaShared);
            }
            finally
            {
                Collections.StringInternCache.Shared = previous;
            }

            Assert.False(TestHelper.HasAnyLogError());
        }

    }
}
