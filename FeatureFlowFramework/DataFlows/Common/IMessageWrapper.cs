namespace FeatureFlowFramework.DataFlows
{
    public interface IMessageWrapper
    {
        object Message { get; set; }
    }

    public interface IMessageWrapper<T> : IMessageWrapper
    {
        T TypedMessage { get; set; }
    }
}
