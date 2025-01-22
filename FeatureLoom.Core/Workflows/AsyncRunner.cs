﻿using System.Threading.Tasks;

namespace FeatureLoom.Workflows
{
    public class AsyncRunner : AbstractRunner
    {
        public override async Task RunAsync(Workflow workflow)
        {
            AddToRunningWorkflows(workflow);
            try
            {                
                while (await workflow.ExecuteNextStepAsync(executionController).ConfigureAwait(false)) ;
            }
            finally
            {
                RemoveFromRunningWorkflows(workflow);
            }
        }
    }
}