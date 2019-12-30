using System;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Helper
{
    public readonly struct TimeFrame
    {
        public readonly DateTime utcStartTime;
        public readonly DateTime utcEndTime;

        public TimeFrame(TimeSpan duration) : this()
        {
            this.utcStartTime = AppTime.Now;
            this.utcEndTime = duration > TimeSpan.Zero ? utcStartTime + duration : utcStartTime;
        }

        public TimeFrame(DateTime startTime, DateTime endTime)
        {
            startTime = startTime.ToUniversalTime();
            endTime = startTime.ToUniversalTime();
            this.utcStartTime = startTime;
            this.utcEndTime = endTime > startTime ? endTime : startTime;
        }

        public TimeFrame(DateTime startTime, TimeSpan duration)
        {
            startTime = startTime.ToUniversalTime();
            this.utcStartTime = startTime;
            this.utcEndTime = duration > TimeSpan.Zero ? startTime + duration : startTime;
        }

        public bool Elapsed => AppTime.Now > utcEndTime;
        public TimeSpan RemainingTime => utcEndTime - AppTime.Now;
        public TimeSpan TimeUntilStart => AppTime.Now - utcStartTime;
        public TimeSpan LapsedTime => AppTime.Now - utcStartTime;
        public TimeSpan Duration => utcEndTime - utcStartTime;
        public bool IsZero => Duration == TimeSpan.Zero;

        public bool IsWithin(TimeFrame otherTimeFrame) => utcStartTime >= otherTimeFrame.utcStartTime && utcEndTime <= otherTimeFrame.utcEndTime;

        public bool Contains(TimeFrame otherTimeFrame) => utcStartTime <= otherTimeFrame.utcStartTime && utcEndTime >= otherTimeFrame.utcEndTime;

        public bool Overlaps(TimeFrame otherTimeFrame) => (utcStartTime >= otherTimeFrame.utcStartTime && utcStartTime < otherTimeFrame.utcEndTime) || (utcEndTime <= otherTimeFrame.utcEndTime && utcEndTime > otherTimeFrame.utcStartTime);

        public Task WaitForStartAsync()
        {
            return Task.Delay(TimeUntilStart.ClampLow(TimeSpan.Zero));
        }

        public Task WaitForEndAsync()
        {
            return Task.Delay(RemainingTime.ClampLow(TimeSpan.Zero));
        }
    }
}
