namespace FeatureLoom.Workflows
{
    public class BlockingRunner : AbstractRunner
    {
        public override void Run(Workflow workflow)
        {
            AddToRunningWorkflows(workflow);
            try
            {
                while(workflow.ExecuteNextStep(executionController)) ;
            }
            finally
            {
                RemoveFromRunningWorkflows(workflow);
            }
        }
    }
}