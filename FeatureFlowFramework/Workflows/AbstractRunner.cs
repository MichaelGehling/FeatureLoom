using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.Helper;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Workflows
{
    public abstract class AbstractRunner : IWorkflowRunner
    {
        RWLock runningWorkflowsLock = new RWLock();
        protected List<IWorkflowControls> runningWorkflows = new List<IWorkflowControls>();
        protected readonly IStepExecutionController executionController = new DefaultStepExecutionController();
        protected Forwarder executionInfoForwarder = null;

        public IEnumerable<IWorkflowControls> RunningWorkflows
        {
            get
            {
                using (runningWorkflowsLock.ForReading())
                {
                    return runningWorkflows.ToArray();
                }
            }
        }

        public IDataFlowSource ExecutionInfoSource
        {
            get
            {
                if (executionInfoForwarder == null)
                {
                    executionInfoForwarder = new Forwarder();
                    using(runningWorkflowsLock.ForReading())
                    {
                        foreach (var workflow in runningWorkflows)
                        {
                            workflow.ExecutionInfoSource.ConnectTo(executionInfoForwarder);
                        }
                    }
                }
                return executionInfoForwarder;
            }
        }

        public Task PauseAllWorkflows()
        {
            List<Task> tasks = new List<Task>();
            using(runningWorkflowsLock.ForReading())
            {
                foreach (var wf in runningWorkflows)
                {
                    wf.RequestPause();
                    tasks.Add(wf.WaitUntilStopsRunningAsync());
                }
            }
            return Task.WhenAll(tasks.ToArray());
        }

        protected void RemoveFromRunningWorkflows(IWorkflowControls workflow)
        {
            using(runningWorkflowsLock.ForWriting())
            {
                runningWorkflows.Remove(workflow);
                if (executionInfoForwarder != null) workflow.ExecutionInfoSource.DisconnectFrom(executionInfoForwarder);
            }
        }

        protected void AddToRunningWorkflows(IWorkflowControls workflow)
        {
            using(runningWorkflowsLock.ForWriting())
            {
                runningWorkflows.Add(workflow);
                if (executionInfoForwarder != null) workflow.ExecutionInfoSource.ConnectTo(executionInfoForwarder);
            }
        }

        public abstract void Run(IWorkflowControls workflow);
    }
}