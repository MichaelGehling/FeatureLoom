namespace FeatureLoom.Workflows
{
    public interface IStateInfo
    {
        IStepInfo[] StepInfos { get; }
        string Name { get; }
        string Description { get; }
        int StateIndex { get; }
    }
}