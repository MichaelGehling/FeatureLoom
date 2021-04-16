using FeatureFlowFramework.Helpers.Misc;
using FeatureFlowFramework.Helpers.Diagnostics;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using FeatureFlowFramework.Helpers.Synchronization;
using FeatureFlowFramework.Services;
using FeatureFlowFramework.Helpers.Time;

namespace FeatureFlowFramework.Helpers
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
