using System;

namespace FeatureFlowFramework.Services.Supervision
{
    public interface ISupervisor
    {
        void Handle();
        bool IsActive { get; }
        TimeSpan MaxDelay { get;  }
    }

}