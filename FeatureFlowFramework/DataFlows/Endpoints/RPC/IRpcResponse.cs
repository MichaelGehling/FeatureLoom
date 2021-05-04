namespace FeatureLoom.DataFlows.RPC
{
    public interface IRpcResponse
    {
        long RequestId { get; }

        string ResultToJson();
    }
}