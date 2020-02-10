using FeatureFlowFramework.Logging;
using System;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Workflows
{
    public class AsyncRunner : AbstractRunner
    {
        public async Task RunAsync(IWorkflowControls workflow)
        {
            AddToRunningWorkflows(workflow);
            try
            {
                while (await workflow.ExecuteNextStepAsync(executionController)) ;
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