namespace FeatureLoom.RPC
{
    public interface IRpcResponse
    {
        long RequestId { get; }

        string ResultToJson();
    }
}