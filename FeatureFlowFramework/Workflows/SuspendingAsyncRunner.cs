using FeatureFlowFramework.Helper;
using FeatureFlowFramework.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Workflows
{
    public class SuspendingAsyncRunner : AbstractRunner
    {
        public SuspendingAsyncRunner()
        {
            this.suspensionTime = 1.Milliseconds();
        }

        public SuspendingAsyncRunner(TimeSpan suspensionTime)
        {
            this.suspensionTime = suspensionTime;
        }

        TimeSpan suspensionTime;

        public async Task RunAsync(IWorkflowControls workflow)
        {
            AddToRunningWorkflows(workflow);
            try
            {
                while(await workflow.ExecuteNextStepAsync(executionController)) 
                {
                    if (SynchronizationContext.Current != null) await Task.Delay(suspensionTime);
                }
            }
            finally
            {
                RemoveFromRunningWorkflows(workflow);
            }
        }

        public override void Run(IWorkflowControls workflow)
        {
            _ = RunAsync(workflow);
        }
    }
}