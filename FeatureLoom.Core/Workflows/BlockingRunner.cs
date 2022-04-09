using System.Threading.Tasks;

namespace FeatureLoom.Workflows
{
    public class BlockingRunner : AbstractRunner
    {
        public override Task RunAsync(Workflow workflow)
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
            return Task.CompletedTask;
        }
    }
}