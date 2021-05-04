using FeatureLoom.DataFlows;

namespace FeatureLoom.Workflows
{
    public interface IWorkflowInfo
    {
        IStateMachineInfo StateMachineInfo { get; }
        Workflow.ExecutionState CurrentExecutionState { get; }
        IDataFlowSource ExecutionInfoSource { get; }
    }
}