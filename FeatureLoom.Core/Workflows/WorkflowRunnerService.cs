﻿using FeatureLoom.MessageFlow;
using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureLoom.Workflows
{
    public static class WorkflowRunnerService
    {
        private static ServiceContext<ContextData> context = new ServiceContext<ContextData>();

        public static IWorkflowRunner DefaultRunner
        {
            get
            {
                if (context.Data.defaultRunner == null) context.Data.defaultRunner = new SmartRunner();
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

        public static IWorkflowRunner[] GetAllRunners()
        {
            using (context.Data.runnersLock.LockReadOnly())
            {
                return context.Data.runners.ToArray();
            }
        }

        public static IReadOnlyList<Workflow> GetAllRunningWorkflows()
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