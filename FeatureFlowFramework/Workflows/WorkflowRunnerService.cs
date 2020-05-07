using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.Helper;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Workflows
{
    public static class WorkflowRunnerService
    {
        static ServiceContext<Context> context = new ServiceContext<Context>();
        public static IWorkflowRunner DefaultRunner
        {
            get
            {
                if(context.Data.defaultRunner == null) context.Data.defaultRunner = new SuspendingAsyncRunner();
                return context.Data.defaultRunner;
            }
        }

        public static void Register(IWorkflowRunner runner)
        {
            using(context.Data.runnersLock.ForWriting())
            {
                context.Data.runners.Add(runner);
                if(context.Data.executionInfoForwarder != null) runner.ExecutionInfoSource.ConnectTo(context.Data.executionInfoForwarder);
            }
        }

        public static void Unregister(IWorkflowRunner runner)
        {
            using(context.Data.runnersLock.ForWriting())
            {
                context.Data.runners.Remove(runner);
                if(context.Data.executionInfoForwarder != null) runner.ExecutionInfoSource.DisconnectFrom(context.Data.executionInfoForwarder);
            }
        }

        public static IWorkflowRunner[] GetAllRunners()
        {
            using(context.Data.runnersLock.ForReading())
            {
                return context.Data.runners.ToArray();
            }
        }

        public static IDataFlowSource ExecutionInfoSource
        {
            get
            {
                if(context.Data.executionInfoForwarder == null)
                {
                    context.Data.executionInfoForwarder = new Forwarder();
                    foreach(var runner in GetAllRunners())
                    {
                        runner.ExecutionInfoSource.ConnectTo(context.Data.executionInfoForwarder);
                    }
                }
                return context.Data.executionInfoForwarder;
            }
        }

        public static async Task PauseAllWorkflows(bool tryCancelWaitingStep)
        {
            List<Task> tasks = new List<Task>();
            foreach(var runner in GetAllRunners())
            {
                tasks.Add(runner.PauseAllWorkflows(tryCancelWaitingStep));
            }
            await Task.WhenAll(tasks.ToArray());
        }

        class Context : IServiceContextData
        {
            public List<IWorkflowRunner> runners = new List<IWorkflowRunner>();
            public FeatureLock runnersLock = new FeatureLock();
            public IWorkflowRunner defaultRunner;
            public Forwarder executionInfoForwarder = null;
            public IServiceContextData Copy()
            {
                using(runnersLock.ForReading())
                {
                    return new Context()
                    {
                        defaultRunner = this.defaultRunner,
                        runners = new List<IWorkflowRunner>(this.runners)
                    };
                }
            }
        }
    }
}