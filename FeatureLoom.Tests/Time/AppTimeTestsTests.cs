using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Threading;
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
        
        [Fact]
        public void WaitTimeIsCorrect()
        {
            TestHelper.PrepareTestContext();

            var tk = AppTime.TimeKeeper;
            AppTime.WaitPrecisely(5.Milliseconds());            
            Assert.True(tk.Elapsed > 5.Milliseconds());
            Assert.True(tk.LastElapsed < 7.Milliseconds());
            

            tk = AppTime.TimeKeeper;
            AppTime.WaitPrecisely(20.Milliseconds());            
            Assert.True(tk.Elapsed > 20.Milliseconds());
            Assert.True(tk.LastElapsed < 25.Milliseconds());
        }

        [Fact]
        public void WaitAsyncTimeIsCorrect()
        {
            TestHelper.PrepareTestContext();

            var tk = AppTime.TimeKeeper;
            AppTime.WaitPreciselyAsync(5.Milliseconds()).WaitFor();
            Assert.True(tk.Elapsed > 5.Milliseconds());
            Assert.True(tk.LastElapsed < 8.Milliseconds());

            tk = AppTime.TimeKeeper;
            AppTime.WaitPreciselyAsync(20.Milliseconds()).WaitFor();
            Assert.True(tk.Elapsed > 20.Milliseconds());
            Assert.True(tk.LastElapsed < 25.Milliseconds());
        }
        
    }
}