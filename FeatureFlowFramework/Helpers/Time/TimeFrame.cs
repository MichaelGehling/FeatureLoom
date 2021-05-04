using FeatureLoom.Helpers.Extensions;
using FeatureLoom.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Helpers.Time
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
        public bool Elapsed() => Elapsed(AppTime.Now);
        public bool Elapsed(DateTime now) => IsZero ? true : now >= utcEndTime;
        public TimeSpan Remaining() => Remaining(AppTime.Now);
        public TimeSpan Remaining(DateTime now) => IsInvalid ? TimeSpan.Zero : utcEndTime - now;
        public TimeSpan TimeUntilStart() => TimeUntilStart(AppTime.Now);
        public TimeSpan TimeUntilStart(DateTime now) => IsInvalid ? TimeSpan.Zero : utcStartTime - now;
        public TimeSpan TimeSinceStart() => TimeSinceStart(AppTime.Now);
        public TimeSpan TimeSinceStart(DateTime now) => IsInvalid ? TimeSpan.Zero : now - utcStartTime;
        public TimeSpan Duration => IsZero ? TimeSpan.Zero : utcEndTime - utcStartTime;
        public bool IsZero => utcEndTime == utcStartTime;

        public bool IsWithin(TimeFrame otherTimeFrame) => IsInvalid ? false : utcStartTime >= otherTimeFrame.utcStartTime && utcEndTime <= otherTimeFrame.utcEndTime;

        public bool Contains(TimeFrame otherTimeFrame) => IsInvalid ? false : utcStartTime <= otherTimeFrame.utcStartTime && utcEndTime >= otherTimeFrame.utcEndTime;

        public bool Overlaps(TimeFrame otherTimeFrame) => IsInvalid ? false : (utcStartTime >= otherTimeFrame.utcStartTime && utcStartTime < otherTimeFrame.utcEndTime) || (utcEndTime <= otherTimeFrame.utcEndTime && utcEndTime > otherTimeFrame.utcStartTime);

        public Task WaitForStartAsync() =>  Task.Delay(TimeUntilStart().ClampLow(TimeSpan.Zero));
        public Task WaitForStartAsync(DateTime now) => Task.Delay(TimeUntilStart(now).ClampLow(TimeSpan.Zero));

        public Task WaitForEndAsync() => Task.Delay(Remaining().ClampLow(TimeSpan.Zero));
        public Task WaitForEndAsync(DateTime now) => Task.Delay(Remaining(now).ClampLow(TimeSpan.Zero));

        public void WaitForStart() => Thread.Sleep(TimeUntilStart().ClampLow(TimeSpan.Zero));
        public void WaitForStart(DateTime now) => Thread.Sleep(TimeUntilStart(now).ClampLow(TimeSpan.Zero));

        public void WaitForEnd() => Thread.Sleep(Remaining().ClampLow(TimeSpan.Zero));
        public void WaitForEnd(DateTime now) => Thread.Sleep(Remaining(now).ClampLow(TimeSpan.Zero));
    }
}