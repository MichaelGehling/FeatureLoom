using System;

namespace FeatureLoom.Scheduling
{
    public interface ISchedule
    {
        void Handle(DateTime now);

        bool IsActive { get; }
        TimeSpan MaxDelay { get; }
    }
}