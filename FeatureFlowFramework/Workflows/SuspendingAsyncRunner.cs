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
            this.suspensionIntervall = 10.Milliseconds();
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
                
                /*while(await workflow.ExecuteNextStepAsync(executionController))
                {
                    if(SynchronizationContext.Current != null && timer.Elapsed > suspensionIntervall)
                    {
                        await Task.Delay(suspensionTime);
                        timer.Restart();
                    }
                }*/

                bool running;
                do
                {
                    Task<bool> stepTask = workflow.ExecuteNextStepAsync(executionController);
                    if (stepTask.IsCompleted && timer.Elapsed > suspensionIntervall)
                    {
                        await Task.Delay(suspensionTime);
                        timer.Restart();
                    }
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