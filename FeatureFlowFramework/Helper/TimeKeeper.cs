using System;

namespace FeatureFlowFramework.Helper
{
    public struct TimeKeeper
    {
        private TimeSpan startTime;

        public TimeKeeper(TimeSpan startTime)
        {
            this.startTime = startTime;
        }

        public TimeSpan Elapsed => AppTime.Elapsed - startTime;

        public void Restart() => startTime = AppTime.Elapsed;
    }
}