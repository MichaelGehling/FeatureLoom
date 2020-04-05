using System.Threading;

namespace FeatureFlowFramework.Workflows
{
    public interface IStateMachineContext
    {
        Workflow.ExecutionState CurrentExecutionState { get; set; }
        Workflow.ExecutionPhase ExecutionPhase { get; set; }
        string ContextName { get; }
        CancellationToken CancellationToken { get; }
        bool PauseRequested { get; set; }
        long ContextId { get; }

        void SendExecutionInfoEvent(string executionEvent, object additionalInfo = null);

        void SendExecutionInfoEvent(string executionEvent, Workflow.ExecutionState state, Workflow.ExecutionPhase phase, object additionalInfo = null);
    }
}