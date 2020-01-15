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
                while(await workflow.ExecuteNextStepAsync(executionController)) await Task.Delay(suspensionTime);
            }
            catch(Exception e)
            {
                Log.ERROR($"Workflow failed! ({workflow.Name})", e.ToString());
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