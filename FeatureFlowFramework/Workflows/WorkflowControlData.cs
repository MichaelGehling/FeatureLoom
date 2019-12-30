using FeatureFlowFramework.Helper;
using System.Threading;

namespace FeatureFlowFramework.Workflows
{
    public struct WorkflowControlData
    {
        public CancellationTokenSource cancellationTokenSource;
        public AsyncManualResetEvent notRunningWakeEvent;
        public SemaphoreSlim semaphore;
        public volatile bool pauseRequested;

        public static WorkflowControlData Init()
        {
            return new WorkflowControlData()
            {
                cancellationTokenSource = null,
                pauseRequested = false,
                notRunningWakeEvent = null,
                semaphore = new SemaphoreSlim(1, 1)
            };
        }
    }
}