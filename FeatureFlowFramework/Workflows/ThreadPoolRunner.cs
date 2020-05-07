using FeatureFlowFramework.Logging;
using System;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Workflows
{
    public class ThreadPoolRunner : AbstractRunner
    {
        public override void Run(Workflow workflow)
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
                    Log.ERROR(this, $"Workflow failed! ({workflow.Name})", e.ToString());
                }
                finally
                {
                    RemoveFromRunningWorkflows(workflow);
                }
            });
        }
    }
}