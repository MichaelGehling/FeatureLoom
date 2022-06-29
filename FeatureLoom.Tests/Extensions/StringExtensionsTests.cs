using FeatureLoom.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using FeatureLoom.UndoRedo;

namespace FeatureLoom.Extensions
{
    
    public class StringExtensionsTests
    {

        [Fact]
        public void TrimStartCanRemoveMultipleFragments()
        {
            TestHelper.PrepareTestContext();

            Assert.Equal("XyzAbc", "XyzAbc".TrimStart("Abc"));
            Assert.Equal("Xyz", "AbcXyz".TrimStart("Abc"));
            Assert.Equal("Xyz", "AbcAbcXyz".TrimStart("Abc"));

            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void TrimEndCanRemoveMultipleFragments()
        {
            TestHelper.PrepareTestContext();

            Assert.Equal("XyzAbc", "XyzAbc".TrimEnd("Xyz"));
            Assert.Equal("Abc", "AbcXyz".TrimEnd("Xyz"));
            Assert.Equal("Abc", "AbcXyzXyz".TrimEnd("Xyz"));

            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void TrimStartCanRemoveWholeString()
        {
            TestHelper.PrepareTestContext();

            Assert.Equal("", "Abc".TrimStart("Abc"));            

            Assert.False(TestHelper.HasAnyLogError());
        }

        [Fact]
        public void TrimEndCanRemoveWholeString()
        {
            TestHelper.PrepareTestContext();

            Assert.Equal("", "Abc".TrimEnd("Abc"));

            Assert.False(TestHelper.HasAnyLogError());
        }

    }
}
