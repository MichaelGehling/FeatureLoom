using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using FeatureLoom.Time;
using System;
using Xunit;

namespace FeatureLoom.Time
{
    public class AppTimeTests
    {
        [Fact]
        public void CoarseTimeIsInLimits()
        {
            TestHelper.PrepareTestContext();

            Assert.True((DateTime.UtcNow - AppTime.CoarseNow).Duration() < 20.Milliseconds());
        }
    }
}