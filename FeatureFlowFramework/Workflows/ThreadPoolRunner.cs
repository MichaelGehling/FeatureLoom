using FeatureFlowFramework.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Workflows
{
    public class ThreadPoolRunner : AbstractRunner
    {  
        public override void Run(IWorkflowControls workflow)
        {            
            Task.Run(() =>
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
            });         
        }
    }
}
