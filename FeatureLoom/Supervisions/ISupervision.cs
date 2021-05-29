using System;

namespace FeatureLoom.Supervisions
{
    public interface ISupervision
    {
        void Handle(DateTime now);

        bool IsActive { get; }
        TimeSpan MaxDelay { get; }
    }
}