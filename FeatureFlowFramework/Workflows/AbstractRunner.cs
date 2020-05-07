using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.Helper;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Workflows
{
    public abstract class AbstractRunner : IWorkflowRunner
    {
        private FeatureLock runningWorkflowsLock = new FeatureLock();
        protected List<Workflow> runningWorkflows = new List<Workflow>();
        protected readonly IStepExecutionController executionController = new DefaultStepExecutionController();
        protected Forwarder executionInfoForwarder = null;

        protected AbstractRunner()
        {
            WorkflowRunnerService.Register(this);
        }

        public IEnumerable<Workflow> RunningWorkflows
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

        public async Task PauseAllWorkflows(bool tryCancelWaitingStep)
        {
            List<Task> tasks = new List<Task>();
            foreach(var wf in RunningWorkflows)
            {
                wf.RequestPause(tryCancelWaitingStep);

                tasks.Add(wf.WaitUntilAsync(info => info.executionPhase != Workflow.ExecutionPhase.Running && info.executionPhase != Workflow.ExecutionPhase.Waiting));
            }
            await Task.WhenAll(tasks.ToArray());
        }

        protected void RemoveFromRunningWorkflows(Workflow workflow)
        {
            using(runningWorkflowsLock.ForWriting())
            {
                runningWorkflows.Remove(workflow);
                if(executionInfoForwarder != null) workflow.ExecutionInfoSource.DisconnectFrom(executionInfoForwarder);
            }
        }

        protected void AddToRunningWorkflows(Workflow workflow)
        {
            workflow.Runner = this;
            using(runningWorkflowsLock.ForWriting())
            {
                runningWorkflows.Add(workflow);
                if(executionInfoForwarder != null) workflow.ExecutionInfoSource.ConnectTo(executionInfoForwarder);
            }
        }

        public abstract void Run(Workflow workflow);
    }
}