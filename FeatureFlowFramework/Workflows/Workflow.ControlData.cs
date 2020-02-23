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
            public volatile bool pauseRequested;

            public static ControlData Init()
            {
                return new ControlData()
                {
                    cancellationTokenSource = null,
                    pauseRequested = false,
                    notRunningWakeEvent = null,
                };
            }
        }
    }
}