namespace FeatureFlowFramework.Workflows
{
    public interface IStepInfo
    {
        string Description { get; }
        IStateInfo[] TargetStates { get; }
        bool MayTerminate { get; }
        int StepIndex { get; }
    }
}