using FeatureLoom.Extensions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Time
{
    public readonly struct TimeFrame
    {
        public readonly DateTime utcStartTime;
        public readonly DateTime utcEndTime;

        public TimeFrame(TimeSpan duration) : this()
        {
            if (duration != default)
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

        public Task WaitForStartAsync()
        {
            var waitTime = TimeUntilStart().ClampLow(TimeSpan.Zero);
            return AppTime.WaitAsync(waitTime, waitTime);
        }

        public Task WaitForStartAsync(DateTime now)
        {
            var waitTime = TimeUntilStart(now).ClampLow(TimeSpan.Zero);
            return AppTime.WaitAsync(waitTime, waitTime);
        }

        public Task WaitForEndAsync()
        {
            var waitTime = Remaining().ClampLow(TimeSpan.Zero);
            return AppTime.WaitAsync(waitTime, waitTime);
        }

    public Task WaitForEndAsync(DateTime now)
        {
            var waitTime = Remaining(now).ClampLow(TimeSpan.Zero);
            return AppTime.WaitAsync(waitTime, waitTime);
        }

        public void WaitForStart()
        {
            var waitTime = TimeUntilStart().ClampLow(TimeSpan.Zero);
            AppTime.Wait(waitTime, waitTime);
        }
        
        public void WaitForStart(DateTime now)
        {
            var waitTime = TimeUntilStart(now).ClampLow(TimeSpan.Zero);
            AppTime.Wait(waitTime, waitTime);
        }

        public void WaitForEnd()
        {
            var waitTime = Remaining().ClampLow(TimeSpan.Zero);
            AppTime.Wait(waitTime, waitTime);
        }

        public void WaitForEnd(DateTime now)
        {
            var waitTime = Remaining(now).ClampLow(TimeSpan.Zero);
            AppTime.Wait(waitTime, waitTime);
        }
    }
}