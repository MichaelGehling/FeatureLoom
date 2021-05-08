using System;

namespace FeatureLoom.Supervisions
{
    public interface ISupervision
    {
        void Handle();

        bool IsActive { get; }
        TimeSpan MaxDelay { get; }
    }
}