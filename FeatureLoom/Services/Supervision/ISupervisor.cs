using System;

namespace FeatureLoom.Services.Supervision
{
    public interface ISupervisor
    {
        void Handle();
        bool IsActive { get; }
        TimeSpan MaxDelay { get;  }
    }

}