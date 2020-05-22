namespace FeatureFlowFramework.Aspects.AppStructure
{
    public interface IAcceptsParent : IHasParent
    {
        new object Parent { get; set; }

        IAcceptsParent SetParent(object parent);
    }
}