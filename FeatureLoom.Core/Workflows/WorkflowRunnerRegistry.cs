using FeatureLoom.MessageFlow;
using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace FeatureLoom.Workflows
{

    //TODO: REMOVE or replace WorkflowRunnerService!
    public interface IWorkflowRunnerRegistry
    {
        IWorkflowRunner DefaultWorkflowRunner { get; }
        void Register(IWorkflowRunner runner);
        void Unregister(IWorkflowRunner runner);
        IEnumerable<IWorkflowRunner> GetAllRunners();
        IEnumerable<Workflow> GetAllRunningWorkflows();
        IMessageSource ExecutionInfoSource { get; }
        Task PauseAllWorkflowsAsync(bool tryCancelWaitingStep);
    }

    public class WorkflowRunnerRegistryInstance : IWorkflowRunnerRegistry        
    {
        List<IWorkflowRunner> runners = new List<IWorkflowRunner>();
        FeatureLock runnersLock = new FeatureLock();
        IWorkflowRunner defaultRunner;
        Forwarder executionInfoForwarder = null;

        public IWorkflowRunner DefaultWorkflowRunner
        {
            get
            {
                if (defaultRunner == null)
                {
                    var newRunner = new SuspendingAsyncRunner();
                    if (Interlocked.CompareExchange(ref defaultRunner, newRunner, null) != null) Unregister(newRunner);
                }
                return defaultRunner;
            }

            set
            {
                defaultRunner = value;
            }
        }

        public IMessageSource ExecutionInfoSource => throw new System.NotImplementedException();

        public IEnumerable<IWorkflowRunner> GetAllRunners()
        {
            throw new System.NotImplementedException();
        }

        public IEnumerable<Workflow> GetAllRunningWorkflows()
        {
            throw new System.NotImplementedException();
        }

        public Task PauseAllWorkflowsAsync(bool tryCancelWaitingStep)
        {
            throw new System.NotImplementedException();
        }

        public void Register(IWorkflowRunner runner)
        {
            throw new System.NotImplementedException();
        }

        public void Unregister(IWorkflowRunner runner)
        {
            throw new System.NotImplementedException();
        }
    }

    public static class WorkflowRunnerRegistry
    {
        private static ServiceContext<ContextData> context = new ServiceContext<ContextData>();

        public static IWorkflowRunner DefaultRunner
        {
            get
            {
                if (context.Data.defaultRunner == null) context.Data.defaultRunner = new SuspendingAsyncRunner();
                return context.Data.defaultRunner;
            }
        }

        public static void Register(IWorkflowRunner runner)
        {
            using (context.Data.runnersLock.Lock())
            {
                context.Data.runners.Add(runner);
                if (context.Data.executionInfoForwarder != null) runner.ExecutionInfoSource.ConnectTo(context.Data.executionInfoForwarder);
            }
        }

        public static void Unregister(IWorkflowRunner runner)
        {
            using (context.Data.runnersLock.Lock())
            {
                context.Data.runners.Remove(runner);
                if (context.Data.executionInfoForwarder != null) runner.ExecutionInfoSource.DisconnectFrom(context.Data.executionInfoForwarder);
            }
        }

        public static IEnumerable<IWorkflowRunner> GetAllRunners()
        {
            using (context.Data.runnersLock.LockReadOnly())
            {
                return context.Data.runners.ToArray();
            }
        }

        public static IEnumerable<Workflow> GetAllRunningWorkflows()
        {
            List<Workflow> workflows = new List<Workflow>();
            foreach (var runner in GetAllRunners())
            {
                workflows.AddRange(runner.RunningWorkflows);
            }
            return workflows;
        }

        public static IMessageSource ExecutionInfoSource
        {
            get
            {
                if (context.Data.executionInfoForwarder == null)
                {
                    context.Data.executionInfoForwarder = new Forwarder();
                    foreach (var runner in GetAllRunners())
                    {
                        runner.ExecutionInfoSource.ConnectTo(context.Data.executionInfoForwarder);
                    }
                }
                return context.Data.executionInfoForwarder;
            }
        }

        public static async Task PauseAllWorkflowsAsync(bool tryCancelWaitingStep)
        {
            List<Task> tasks = new List<Task>();
            foreach (var runner in GetAllRunners())
            {
                tasks.Add(runner.PauseAllWorkflows(tryCancelWaitingStep));
            }
            await Task.WhenAll(tasks.ToArray());
        }

        private class ContextData : IServiceContextData
        {
            public List<IWorkflowRunner> runners = new List<IWorkflowRunner>();
            public FeatureLock runnersLock = new FeatureLock();
            public IWorkflowRunner defaultRunner;
            public Forwarder executionInfoForwarder = null;

            public IServiceContextData Copy()
            {
                using (runnersLock.LockReadOnly())
                {
                    return new ContextData()
                    {
                        defaultRunner = this.defaultRunner,
                        runners = new List<IWorkflowRunner>(this.runners)
                    };
                }
            }
        }
    }
}