using FeatureFlowFramework.DataFlows;
using System;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Workflows
{
    public interface IWorkflowControls
    {
        Workflow.ExecutionState CurrentExecutionState { get; set; }
        string Name { get; }

        Task<bool> ExecuteNextStepAsync(IStepExecutionController controller);

        bool ExecuteNextStep(IStepExecutionController controller);

        void Run(IWorkflowRunner runner = null);

        void RequestPause(bool tryCancelWaitingStep = true);

        bool WaitUntilStopsRunning(TimeSpan timeout = default);

        Task<bool> WaitUntilStopsRunningAsync(TimeSpan timeout = default);

        bool IsRunning { get; }

        IDataFlowSource ExecutionInfoSource { get; }

        IWorkflowRunner Runner { get; set; }
    }
}