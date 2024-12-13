using FeatureLoom.Extensions;
using FeatureLoom.Time;
using System;

namespace FeatureLoom.Scheduling
{
    public readonly struct ScheduleStatus
    {
        public static readonly ScheduleStatus Terminated = new ScheduleStatus();
        
        private readonly TimeFrame timeFrame;

        public ScheduleStatus(TimeFrame timeFrame)
        {
            this.timeFrame = timeFrame;
        }

        public ScheduleStatus(TimeSpan minDelay, TimeSpan maxDelay)
        {            
            this.timeFrame = new TimeFrame(minDelay, maxDelay - minDelay);
        }

        public ScheduleStatus(DateTime earliestTriggerTime, DateTime latestTriggerTime)
        {
            this.timeFrame = new TimeFrame(earliestTriggerTime, latestTriggerTime);
        }

        public bool IsTerminated => timeFrame.IsInvalid;
        public TimeFrame ExecutionTimeFrame => timeFrame;

        public static implicit operator ScheduleStatus(TimeFrame timeFrame) => new ScheduleStatus(timeFrame);
        public static ScheduleStatus WaitFor(TimeSpan minDelay) => new ScheduleStatus(minDelay, minDelay + 15.Milliseconds());
        public static ScheduleStatus WaitExactlyFor(TimeSpan minDelay) => new ScheduleStatus(minDelay, minDelay);
        public static ScheduleStatus WaitUntil(DateTime minDate) => new ScheduleStatus(minDate, minDate + 15.Milliseconds());
        public static ScheduleStatus WaitExactlyUntil(DateTime minDate) => new ScheduleStatus(minDate, minDate);
    }
}