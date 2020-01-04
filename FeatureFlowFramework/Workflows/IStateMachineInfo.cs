namespace FeatureFlowFramework.Workflows
{
    public interface IStateMachineInfo
    {
        IStateInfo[] StateInfos { get; }
        string Name { get; }
        IStateInfo StartStateInfo { get; }
    }
}