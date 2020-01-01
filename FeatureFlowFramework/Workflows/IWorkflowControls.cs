using FeatureFlowFramework.DataFlows;
using System;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Workflows
{
    public interface IWorkflowControls
    {
        WorkflowExecutionState ExecutionState { get; set; }
        string Name { get; }

        Task<bool> ExecuteNextStepAsync(IStepExecutionController controller, TimeSpan timeout = default);

        bool ExecuteNextStep(IStepExecutionController controller, TimeSpan timeout = default);

        void Run(IWorkflowRunner runner = null);

        void RequestPause(bool tryCancelWaitingStep = true);

        bool WaitUntilStopsRunning(TimeSpan timeout = default);

        Task<bool> WaitUntilStopsRunningAsync(TimeSpan timeout = default);

        bool IsRunning { get; }

        bool TryLock(TimeSpan timeout = default);

        Task<bool> TryLockAsync(TimeSpan timeout = default);

        void Unlock();

        bool LockAndExecute(Action action, TimeSpan timeout = default);

        Task<bool> LockAndExecuteAsync(Action action, TimeSpan timeout = default);

        Task<bool> LockAndExecuteAsync(Func<Task> action, TimeSpan timeout = default);

        IDataFlowSource ExecutionInfoSource { get; }
    }
}