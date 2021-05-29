using FeatureLoom.MessageFlow;

namespace FeatureLoom.Workflows
{
    public interface IWorkflowInfo
    {
        IStateMachineInfo StateMachineInfo { get; }
        Workflow.ExecutionState CurrentExecutionState { get; }
        IMessageSource ExecutionInfoSource { get; }
    }
}