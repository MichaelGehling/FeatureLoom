using FeatureLoom.MessageFlow;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeatureLoom.Workflows
{
    public interface IWorkflowRunner
    {
        void Run(Workflow workflow);

        IEnumerable<Workflow> RunningWorkflows { get; }

        Task PauseAllWorkflows(bool tryCancelWaitingStep);

        IMessageSource ExecutionInfoSource { get; }
    }
}