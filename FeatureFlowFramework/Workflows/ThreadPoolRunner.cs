using FeatureLoom.Services.Logging;
using FeatureLoom.Services.MetaData;
using System;
using System.Threading.Tasks;

namespace FeatureLoom.Workflows
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
                    Log.ERROR(this.GetHandle(), $"Workflow failed! ({workflow.Name})", e.ToString());
                }
                finally
                {
                    RemoveFromRunningWorkflows(workflow);
                }
            });
        }
    }
}