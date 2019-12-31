using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Workflows
{
    public class BlockingRunner : AbstractRunner
    {
        public override void Run(IWorkflowControls workflow)
        {
            AddToRunningWorkflows(workflow);
            try
            {
                while(workflow.ExecuteNextStep(executionController)) ;
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
    }
}
