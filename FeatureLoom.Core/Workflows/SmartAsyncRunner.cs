using System.Threading.Tasks;

namespace FeatureLoom.Workflows
{
    public class SmartAsyncRunner : AbstractRunner
    {
        public override async Task RunAsync(Workflow workflow)
        {
            AddToRunningWorkflows(workflow);
            try
            {
                bool running;
                do
                {
                    if (workflow.IsNextStepAsync()) running = await workflow.ExecuteNextStepAsync(executionController);
                    else running = workflow.ExecuteNextStep(executionController);
                }
                while (running);
            }
            finally
            {
                RemoveFromRunningWorkflows(workflow);
            }
        }
    }
}