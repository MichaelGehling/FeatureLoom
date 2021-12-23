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

        public TimeFrame(TimeSpan duration)
        {
            if (duration >= TimeSpan.Zero)
            {
                this.utcStartTime = duration >= 1.Seconds() ? AppTime.CoarseNow : AppTime.Now;
                this.utcEndTime = utcStartTime + duration;
            }
            else
            {
                this.utcEndTime = duration >= 1.Seconds() ? AppTime.CoarseNow : AppTime.Now;
                this.utcStartTime = utcEndTime + duration;
            }
        }

        public TimeFrame(DateTime startTime, DateTime endTime)
        {
            startTime = startTime.ToUniversalTime();
            endTime = startTime.ToUniversalTime();            
            this.utcStartTime = endTime > startTime ? startTime : endTime;
            this.utcEndTime = endTime > startTime ? endTime : startTime;
        }

        public TimeFrame(DateTime startTime, TimeSpan duration)
        {
            if (duration >= TimeSpan.Zero)
            {
                this.utcStartTime = startTime.ToUniversalTime();
                this.utcEndTime = utcStartTime + duration;
            }
            else
            {
                this.utcEndTime = startTime.ToUniversalTime();
                this.utcStartTime = utcEndTime + duration;
            }
        }

        public bool IsInvalid => utcStartTime == default;

        public bool Elapsed() => Elapsed(Duration >= 1.Seconds() ? AppTime.CoarseNow :AppTime.Now);

        public bool Elapsed(DateTime now) => now >= utcEndTime;

        public TimeSpan Remaining() => Remaining(Duration >= 1.Seconds() ? AppTime.CoarseNow : AppTime.Now);

        public TimeSpan Remaining(DateTime now) => IsInvalid ? TimeSpan.Zero : utcEndTime - now;

        public TimeSpan TimeUntilStart() => TimeUntilStart(Duration >= 1.Seconds() ? AppTime.CoarseNow : AppTime.Now);

        public TimeSpan TimeUntilStart(DateTime now) => IsInvalid ? TimeSpan.Zero : utcStartTime - now;

        public TimeSpan TimeSinceStart() => TimeSinceStart(Duration >= 1.Seconds() ? AppTime.CoarseNow : AppTime.Now);

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