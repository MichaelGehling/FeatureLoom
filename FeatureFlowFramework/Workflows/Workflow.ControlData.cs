using FeatureFlowFramework.Helper;
using System.Threading;

namespace FeatureFlowFramework.Workflows
{
    public partial class Workflow
    {
        public struct ControlData
        {
            public CancellationTokenSource cancellationTokenSource;
            public AsyncManualResetEvent notRunningWakeEvent;
            public SemaphoreSlim semaphore;
            public volatile bool pauseRequested;

            public static ControlData Init()
            {
                return new ControlData()
                {
                    cancellationTokenSource = null,
                    pauseRequested = false,
                    notRunningWakeEvent = null,
                    semaphore = new SemaphoreSlim(1, 1)
                };
            }
        }
    }
}