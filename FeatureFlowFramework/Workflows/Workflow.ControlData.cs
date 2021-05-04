using FeatureLoom.Helpers;
using System.Threading;

namespace FeatureLoom.Workflows
{
    public partial class Workflow
    {
        public struct ControlData
        {
            public CancellationTokenSource cancellationTokenSource;
            public volatile bool pauseRequested;

            public static ControlData Init()
            {
                return new ControlData()
                {
                    cancellationTokenSource = null,
                    pauseRequested = false,
                };
            }
        }
    }
}