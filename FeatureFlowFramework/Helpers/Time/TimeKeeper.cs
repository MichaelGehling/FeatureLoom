using FeatureFlowFramework.Services;
using System;

namespace FeatureFlowFramework.Helpers.Time
{
    public struct TimeKeeper
    {
        private TimeSpan startTime;
        private TimeSpan lastElapsed;

        public TimeKeeper(TimeSpan startTime)
        {
            this.startTime = startTime;
        }

        public TimeSpan Elapsed
        {
            get
            {
                lastElapsed = AppTime.Elapsed - startTime;
                return lastElapsed;
            }
        }

        public TimeSpan LastElapsed => lastElapsed;

        public void Restart() => startTime = AppTime.Elapsed;
    }
}