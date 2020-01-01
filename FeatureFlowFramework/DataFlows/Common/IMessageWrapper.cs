namespace FeatureFlowFramework.DataFlows
{
    public interface IMessageWrapper
    {
        object Message { get; }
    }

    public interface IMessageWrapper<T> : IMessageWrapper
    {
        T TypedMessage { get; }
    }
}
