namespace FeatureFlowFramework.Aspects.AppStructure
{
    public interface IAcceptsName : IHasName
    {
        new string Name { get; set; }
        IAcceptsName SetName(string name);
    }

}
