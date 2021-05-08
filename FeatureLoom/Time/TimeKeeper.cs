using System;

namespace FeatureLoom.Time
{
    public readonly struct TimeKeeper
    {
        private readonly TimeSpan startTime;

        public TimeKeeper(TimeSpan startTime)
        {
            this.startTime = startTime;
        }

        public TimeSpan Elapsed => AppTime.Elapsed - startTime;

        public TimeSpan StartTime => startTime;
    }
}