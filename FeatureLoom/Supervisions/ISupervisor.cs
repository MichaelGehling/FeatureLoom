using System;

namespace FeatureLoom.Supervisions
{
    public interface ISupervisor
    {
        void Handle();

        bool IsActive { get; }
        TimeSpan MaxDelay { get; }
    }
}