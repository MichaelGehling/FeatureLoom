using FeatureLoom.Diagnostics;
using FeatureLoom.Time;
using Xunit;

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