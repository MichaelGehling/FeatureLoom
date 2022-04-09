using FeatureLoom.MessageFlow;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureLoom.Workflows
{
    public interface IWorkflowRunner
    {
        Task RunAsync(Workflow workflow);

        IEnumerable<Workflow> RunningWorkflows { get; }

        Task PauseAllWorkflows(bool tryCancelWaitingStep);

        IMessageSource ExecutionInfoSource { get; }
    }
}