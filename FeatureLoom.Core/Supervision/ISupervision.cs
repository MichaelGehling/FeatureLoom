using System;

namespace FeatureLoom.Supervision
{
    public interface ISupervision
    {
        void Handle(DateTime now);

        bool IsActive { get; }
        TimeSpan MaxDelay { get; }
    }
}