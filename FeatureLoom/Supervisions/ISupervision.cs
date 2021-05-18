using System;

namespace FeatureLoom.Supervisions
{
    public interface ISupervision
    {
        void Handle(TimeSpan lastDelay);

        bool IsActive { get; }
        TimeSpan MaxDelay { get; }
    }
}