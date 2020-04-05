using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.Helper;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Workflows
{
    public abstract class AbstractRunner : IWorkflowRunner
    {
        private FeatureLock runningWorkflowsLock = new FeatureLock();
        protected List<IWorkflowControls> runningWorkflows = new List<IWorkflowControls>();
        protected readonly IStepExecutionController executionController = new DefaultStepExecutionController();
        protected Forwarder executionInfoForwarder = null;

        public IEnumerable<IWorkflowControls> RunningWorkflows
        {
            get
            {
                using(runningWorkflowsLock.ForReading())
                {
                    return runningWorkflows.ToArray();
                }
            }
        }

        public IDataFlowSource ExecutionInfoSource
        {
            get
            {
                if(executionInfoForwarder == null)
                {
                    executionInfoForwarder = new Forwarder();
                    foreach(var workflow in RunningWorkflows)
                    {
                        workflow.ExecutionInfoSource.ConnectTo(executionInfoForwarder);
                    }
                }
                return executionInfoForwarder;
            }
        }

        public async Task PauseAllWorkflows()
        {
            List<Task> tasks = new List<Task>();
            foreach(var wf in RunningWorkflows)
            {
                wf.RequestPause();

                tasks.Add(wf.WaitUntilStopsRunningAsync());
            }
            await Task.WhenAll(tasks.ToArray());
        }

        protected void RemoveFromRunningWorkflows(IWorkflowControls workflow)
        {
            using(runningWorkflowsLock.ForWriting())
            {
                runningWorkflows.Remove(workflow);
                if(executionInfoForwarder != null) workflow.ExecutionInfoSource.DisconnectFrom(executionInfoForwarder);
            }
        }

        protected void AddToRunningWorkflows(IWorkflowControls workflow)
        {
            workflow.Runner = this;
            using(runningWorkflowsLock.ForWriting())
            {
                runningWorkflows.Add(workflow);
                if(executionInfoForwarder != null) workflow.ExecutionInfoSource.ConnectTo(executionInfoForwarder);
            }
        }

        public abstract void Run(IWorkflowControls workflow);
    }
}