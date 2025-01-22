using FeatureLoom.MessageFlow;
using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureLoom.Workflows
{
    public class WorkflowRunnerService
    {
        List<IWorkflowRunner> runners = new List<IWorkflowRunner>();
        FeatureLock runnersLock = new FeatureLock();
        IWorkflowRunner defaultRunner;
        Forwarder executionInfoForwarder = null;

        public IWorkflowRunner DefaultRunner
        {
            get
            {
                if (defaultRunner == null) defaultRunner = new SmartRunner();
                return defaultRunner;
            }
        }

        public void Register(IWorkflowRunner runner)
        {
            using (runnersLock.Lock())
            {
                runners.Add(runner);
                if (executionInfoForwarder != null) runner.ExecutionInfoSource.ConnectTo(executionInfoForwarder);
            }
        }

        public void Unregister(IWorkflowRunner runner)
        {
            using (runnersLock.Lock())
            {
                runners.Remove(runner);
                if (executionInfoForwarder != null) runner.ExecutionInfoSource.DisconnectFrom(executionInfoForwarder);
            }
        }

        public IWorkflowRunner[] GetAllRunners()
        {
            using (runnersLock.LockReadOnly())
            {
                return runners.ToArray();
            }
        }

        public IReadOnlyList<Workflow> GetAllRunningWorkflows()
        {
            List<Workflow> workflows = new List<Workflow>();
            foreach (var runner in GetAllRunners())
            {
                workflows.AddRange(runner.RunningWorkflows);
            }
            return workflows;
        }

        public IMessageSource ExecutionInfoSource
        {
            get
            {
                if (executionInfoForwarder == null)
                {
                    executionInfoForwarder = new Forwarder();
                    foreach (var runner in GetAllRunners())
                    {
                        runner.ExecutionInfoSource.ConnectTo(executionInfoForwarder);
                    }
                }
                return executionInfoForwarder;
            }
        }

        public async Task PauseAllWorkflowsAsync(bool tryCancelWaitingStep)
        {
            List<Task> tasks = new List<Task>();
            foreach (var runner in GetAllRunners())
            {
                tasks.Add(runner.PauseAllWorkflows(tryCancelWaitingStep));
            }
            await Task.WhenAll(tasks.ToArray()).ConfigureAwait(false);
        }

    }
}