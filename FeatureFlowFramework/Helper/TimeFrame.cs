using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Helper
{
    public readonly struct TimeFrame
    {
        public readonly DateTime utcStartTime;
        public readonly DateTime utcEndTime;

        public TimeFrame(TimeSpan duration) : this()
        {
            if(duration != default)
            {
                this.utcStartTime = AppTime.Now;
                this.utcEndTime = duration > TimeSpan.Zero ? utcStartTime + duration : utcStartTime;
            }
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

        public bool IsInvalid => utcStartTime == default;
        public bool Elapsed => IsZero ? true : AppTime.Now > utcEndTime;
        public TimeSpan Remaining => IsInvalid ? TimeSpan.Zero : utcEndTime - AppTime.Now;
        public TimeSpan TimeUntilStart => IsInvalid ? TimeSpan.Zero : AppTime.Now - utcStartTime;
        public TimeSpan LapsedTime => IsInvalid ? TimeSpan.Zero : AppTime.Now - utcStartTime;
        public TimeSpan Duration => IsInvalid ? TimeSpan.Zero : utcEndTime - utcStartTime;
        public bool IsZero => Duration == TimeSpan.Zero;

        public bool IsWithin(TimeFrame otherTimeFrame) => IsInvalid ? false : utcStartTime >= otherTimeFrame.utcStartTime && utcEndTime <= otherTimeFrame.utcEndTime;

        public bool Contains(TimeFrame otherTimeFrame) => IsInvalid ? false : utcStartTime <= otherTimeFrame.utcStartTime && utcEndTime >= otherTimeFrame.utcEndTime;

        public bool Overlaps(TimeFrame otherTimeFrame) => IsInvalid ? false : (utcStartTime >= otherTimeFrame.utcStartTime && utcStartTime < otherTimeFrame.utcEndTime) || (utcEndTime <= otherTimeFrame.utcEndTime && utcEndTime > otherTimeFrame.utcStartTime);

        public Task WaitForStartAsync()
        {
            return Task.Delay(TimeUntilStart.ClampLow(TimeSpan.Zero));
        }

        public Task WaitForEndAsync()
        {
            return Task.Delay(Remaining.ClampLow(TimeSpan.Zero));
        }

        public void WaitForStart()
        {
            Thread.Sleep(TimeUntilStart.ClampLow(TimeSpan.Zero));
        }

        public void WaitForEnd()
        {
            Thread.Sleep(Remaining.ClampLow(TimeSpan.Zero));
        }
    }
}