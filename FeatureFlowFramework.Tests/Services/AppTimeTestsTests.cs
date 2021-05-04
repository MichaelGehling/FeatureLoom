using FeatureLoom.Helpers.Misc;
using FeatureLoom.Helpers.Diagnostics;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using FeatureLoom.Helpers.Synchronization;
using FeatureLoom.Services;
using FeatureLoom.Helpers.Time;

namespace FeatureLoom.Helpers
{
    public class AppTimeTests
    {
        [Fact]
        public void CoarseTimeIsInLimits()
        {
            TestHelper.PrepareTestContext();

            Assert.True((AppTime.Now - AppTime.CoarseNow).Duration() < 20.Milliseconds());

        }
    }
}
