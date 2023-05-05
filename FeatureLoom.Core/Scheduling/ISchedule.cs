using FeatureLoom.Time;
using System;

namespace FeatureLoom.Scheduling
{
    public interface ISchedule
    {
        ScheduleStatus Trigger(DateTime now);

        string Name { get; }
    }
}