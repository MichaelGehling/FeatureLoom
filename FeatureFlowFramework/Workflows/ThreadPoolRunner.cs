using FeatureFlowFramework.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Workflows
{
    public class ThreadPoolRunner : IWorkflowRunner
    {
        private List<IWorkflowControls> runningWorkflows = new List<IWorkflowControls>();
        private readonly IStepExecutionController executionController = new DefaultStepExecutionController();

        public IEnumerable<IWorkflowControls> RunningWorkflows
        {
            get
            {
                lock(runningWorkflows)
                {
                    return runningWorkflows.ToArray();
                }
            }
        }

        public void Run(IWorkflowControls workflow)
        {
            lock(runningWorkflows)
            {
                runningWorkflows.Add(workflow);
            }

            Task.Run(() =>
            {
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
                    lock(runningWorkflows)
                    {
                        runningWorkflows.Remove(workflow);
                    }
                }
            });         
        }

        public Task PauseAllWorkflows()
        {
            List<Task> tasks = new List<Task>();
            foreach (var wf in RunningWorkflows)
            {
                wf.RequestPause();
                tasks.Add(wf.WaitUntilStopsRunningAsync());
            }
            return Task.WhenAll(tasks.ToArray());
        }
    }
}
