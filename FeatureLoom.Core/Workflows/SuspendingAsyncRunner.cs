using FeatureLoom.Time;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Workflows
{
    public class SuspendingAsyncRunner : AbstractRunner
    {
        public SuspendingAsyncRunner()
        {
            this.suspensionTime = 10.Milliseconds();
            this.suspensionIntervall = 100.Milliseconds();
        }

        public SuspendingAsyncRunner(TimeSpan suspensionTime, TimeSpan suspensionIntervall)
        {
            this.suspensionTime = suspensionTime;
            this.suspensionIntervall = suspensionIntervall;
        }

        private readonly TimeSpan suspensionTime;
        private readonly TimeSpan suspensionIntervall;

        public override async Task RunAsync(Workflow workflow)
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
                        if (timer.Elapsed > suspensionIntervall)
                        {
                            var syncContext = SynchronizationContext.Current;
                            if (syncContext != null && syncContext.GetType() != typeof(SynchronizationContext))
                            {
                                if (suspensionTime == TimeSpan.Zero) await Task.Yield();
                                else await Task.Delay(suspensionTime);
                            }
                            else
                            {
                                ThreadPool.GetAvailableThreads(out int availableThreads, out _);
                                if (availableThreads == 0)
                                {
                                    if (suspensionTime == TimeSpan.Zero) await Task.Yield();
                                    else await Task.Delay(suspensionTime);
                                }
                            }
                            timer = new TimeKeeper(); // When suspension was forced, the suspensionTimer can be reset.
                        }
                    }
                    else timer = new TimeKeeper(); // When step ran async the suspensionTimer can be reset.

                    running = await stepTask;
                } while (running);
            }
            finally
            {
                RemoveFromRunningWorkflows(workflow);
            }
        }

    }
}