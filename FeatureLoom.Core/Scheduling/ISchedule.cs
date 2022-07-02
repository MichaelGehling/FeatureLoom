using FeatureLoom.Time;
using System;

namespace FeatureLoom.Scheduling
{
    public interface ISchedule
    {
        TimeFrame Trigger(DateTime now);

        string Name { get; }
    }
}