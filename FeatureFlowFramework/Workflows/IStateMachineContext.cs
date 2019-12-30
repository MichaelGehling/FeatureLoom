using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Workflows
{
    public interface IStateMachineContext
    {
        WorkflowExecutionState ExecutionState { get; set; }
        WorkflowExecutionPhase ExecutionPhase { get; set; }
        string ContextName { get; }
        CancellationToken CancellationToken { get; }
        bool PauseRequested { get; set; }
        long ContextId { get; }
        void SendExecutionInfoEvent(string executionEvent);
        void Unlock();
        bool TryLock(TimeSpan timeout);
        Task<bool> TryLockAsync(TimeSpan timeout);
    }
}
