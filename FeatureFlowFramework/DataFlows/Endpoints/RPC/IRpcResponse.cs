namespace FeatureFlowFramework.DataFlows.RPC
{
    public interface IRpcResponse
    {
        long RequestId { get; }

        string ResultToJson();
    }
}