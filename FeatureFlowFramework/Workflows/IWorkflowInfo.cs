using FeatureFlowFramework.DataFlows;

namespace FeatureFlowFramework.Workflows
{
    public interface IWorkflowInfo
    {
        IStateMachineInfo StateMachineInfo { get; }
        Workflow.ExecutionState CurrentExecutionState { get; }
        IDataFlowSource ExecutionInfoSource { get; }
    }
}