using FeatureFlowFramework.Logging;
using System;

namespace FeatureFlowFramework.Workflows
{
    public class BlockingRunner : AbstractRunner
    {
        public override void Run(IWorkflowControls workflow)
        {
            AddToRunningWorkflows(workflow);
            try
            {
                while (workflow.ExecuteNextStep(executionController)) ;
            }
            finally
            {
                RemoveFromRunningWorkflows(workflow);
            }
        }
    }
}