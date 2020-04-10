using FeatureFlowFramework.Helper;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Workflows
{
    public class SuspendingAsyncRunner : AbstractRunner
    {
        public SuspendingAsyncRunner()
        {
            this.suspensionTime = 1.Milliseconds();
            this.suspensionIntervall = 100.Milliseconds();
        }

        public SuspendingAsyncRunner(TimeSpan suspensionTime, TimeSpan suspensionIntervall)
        {
            this.suspensionTime = suspensionTime;
            this.suspensionIntervall = suspensionIntervall;
        }

        private readonly TimeSpan suspensionTime;
        private readonly TimeSpan suspensionIntervall;

        public async Task RunAsync(IWorkflowControls workflow)
        {
            AddToRunningWorkflows(workflow);
            try
            {
                var timer = AppTime.TimeKeeper;

                bool running;
                do
                {
                    Task<bool> stepTask = workflow.ExecuteNextStepAsync(executionController);
                    // If step is already completed, it was executed synchronously.                  
                    if (stepTask.IsCompleted)
                    {
                        // If also the suspension intervall is elapsed a suspension has to be performed.
                        if(timer.Elapsed > suspensionIntervall)
                        {
                            await Task.Delay(suspensionTime);
                            timer.Restart(); // When suspension was forced, the suspensionTimer can be reset.
                        }
                    }    
                    else timer.Restart(); // When step ran async the suspensionTimer can be reset.

                    running = await stepTask;
                } while (running);                
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