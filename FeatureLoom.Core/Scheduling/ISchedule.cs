using System;

namespace FeatureLoom.Scheduling
{
    public interface ISchedule
    {
        bool Trigger(DateTime now, out TimeSpan maxDelay);
    }
}