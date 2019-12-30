namespace FeatureFlowFramework.Aspects.AppStructure
{
    public interface IAcceptsDescription : IHasDescription
    {
        new string Description { get; set; }
        IAcceptsDescription SetDescription(string description);
    }

}
