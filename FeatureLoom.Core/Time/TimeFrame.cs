using FeatureLoom.Extensions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Time
{
    public struct TimeFrame
    {
        public readonly DateTime utcStartTime;
        public readonly DateTime utcEndTime;
        private DateTime lastTimeSample;

        public static TimeFrame Invalid => new TimeFrame();

        public DateTime LastTimeSample => lastTimeSample == default ? GetTime() : lastTimeSample;

        private DateTime GetTime()
        {
            lastTimeSample = Duration >= 1.Seconds() ? AppTime.CoarseNow : AppTime.Now;
            return lastTimeSample;
        }

        private DateTime GetTime(TimeSpan duration)
        {
            lastTimeSample = duration >= 1.Seconds() ? AppTime.CoarseNow : AppTime.Now;
            return lastTimeSample;
        }

        public TimeFrame(TimeSpan duration)
        {
            this.utcStartTime = default;
            this.utcEndTime = default;
            lastTimeSample = default;

            if (duration >= TimeSpan.Zero)
            {
                this.utcStartTime = GetTime(duration);
                duration = duration.ClampHigh(DateTime.MaxValue.Subtract(utcStartTime));
                this.utcEndTime = utcStartTime + duration;                
            }
            else
            {
                this.utcEndTime = GetTime(duration);
                duration = duration.ClampLow(DateTime.MaxValue.Subtract(utcEndTime));
                this.utcStartTime = utcEndTime + duration;
            }
        }

        public TimeFrame(DateTime startTime, DateTime endTime)
        {
            this.utcStartTime = default;
            this.utcEndTime = default;
            lastTimeSample = default;

            startTime = startTime.ToUniversalTime();
            endTime = endTime.ToUniversalTime();            
            this.utcStartTime = endTime > startTime ? startTime : endTime;
            this.utcEndTime = endTime > startTime ? endTime : startTime;
        }

        public TimeFrame(DateTime startTime, TimeSpan duration)
        {
            this.utcStartTime = default;
            this.utcEndTime = default;
            lastTimeSample = default;

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

        public TimeFrame(TimeSpan delay, TimeSpan duration)
        {
            this.utcStartTime = default;
            this.utcEndTime = default;
            lastTimeSample = default;

            if (duration >= TimeSpan.Zero)
            {
                this.utcStartTime = GetTime(duration) + delay;
                this.utcEndTime = utcStartTime + duration;
            }
            else
            {
                this.utcEndTime = GetTime(duration) + delay;
                this.utcStartTime = utcEndTime + duration;
            }
        }

        public bool IsInvalid => utcStartTime == default || utcEndTime == default;
        public bool IsValid => !IsInvalid;

        public bool Elapsed() => Elapsed(GetTime());

        public bool Elapsed(DateTime now) => now >= utcEndTime;

        public bool Started() => Started(GetTime());

        public bool Started(DateTime now) => now >= utcStartTime;

        public TimeSpan Remaining() => Remaining(GetTime());

        public TimeSpan Remaining(DateTime now) => IsInvalid ? TimeSpan.Zero : utcEndTime - now;

        public TimeSpan TimeUntilStart() => TimeUntilStart(GetTime());

        public TimeSpan TimeUntilStart(DateTime now) => IsInvalid ? TimeSpan.Zero : utcStartTime - now;

        public TimeSpan TimeSinceStart() => TimeSinceStart(GetTime());

        public TimeSpan TimeSinceStart(DateTime now) => IsInvalid ? TimeSpan.Zero : now - utcStartTime;

        public TimeSpan Duration => IsZero ? TimeSpan.Zero : utcEndTime - utcStartTime;
        public bool IsZero => utcEndTime == utcStartTime;

        public bool IsWithin(TimeFrame otherTimeFrame) => IsInvalid ? false : utcStartTime >= otherTimeFrame.utcStartTime && utcEndTime <= otherTimeFrame.utcEndTime;

        public bool Contains(TimeFrame otherTimeFrame) => IsInvalid ? false : utcStartTime <= otherTimeFrame.utcStartTime && utcEndTime >= otherTimeFrame.utcEndTime;

        public bool Overlaps(TimeFrame otherTimeFrame) => IsInvalid ? false : (utcStartTime >= otherTimeFrame.utcStartTime && utcStartTime < otherTimeFrame.utcEndTime) || (utcEndTime <= otherTimeFrame.utcEndTime && utcEndTime > otherTimeFrame.utcStartTime);

        public Task WaitForStartAsync(bool precisely = false)
        {
            var waitTime = TimeUntilStart().ClampLow(TimeSpan.Zero);
            if (precisely) return AppTime.WaitPreciselyAsync(waitTime);
            else return AppTime.WaitAsync(waitTime);
        }

        public Task WaitForStartAsync(DateTime now, bool precisely = false)
        {
            var waitTime = TimeUntilStart(now).ClampLow(TimeSpan.Zero);
            if (precisely) return AppTime.WaitPreciselyAsync(waitTime);
            else return AppTime.WaitAsync(waitTime);
        }

        public Task WaitForEndAsync(bool precisely = false)
        {
            var waitTime = Remaining().ClampLow(TimeSpan.Zero);
            if (precisely) return AppTime.WaitPreciselyAsync(waitTime);
            else return AppTime.WaitAsync(waitTime);
        }

    public Task WaitForEndAsync(DateTime now, bool precisely = false)
        {
            var waitTime = Remaining(now).ClampLow(TimeSpan.Zero);
            if (precisely) return AppTime.WaitPreciselyAsync(waitTime);
            else return AppTime.WaitAsync(waitTime);
        }

        public void WaitForStart(bool precisely = false)
        {
            var waitTime = TimeUntilStart().ClampLow(TimeSpan.Zero);
            if (precisely) AppTime.WaitPrecisely(waitTime);
            else AppTime.Wait(waitTime);
        }
        
        public void WaitForStart(DateTime now, bool precisely = false)
        {
            var waitTime = TimeUntilStart(now).ClampLow(TimeSpan.Zero);
            if (precisely) AppTime.WaitPrecisely(waitTime);
            else AppTime.Wait(waitTime);
        }

        public void WaitForEnd(bool precisely = false)
        {
            var waitTime = Remaining().ClampLow(TimeSpan.Zero);
            if (precisely) AppTime.WaitPrecisely(waitTime);
            else AppTime.Wait(waitTime);
        }

        public void WaitForEnd(DateTime now, bool precisely = false)
        {
            var waitTime = Remaining(now).ClampLow(TimeSpan.Zero);
            if (precisely) AppTime.WaitPrecisely(waitTime);
            else AppTime.Wait(waitTime);
        }
    }
}