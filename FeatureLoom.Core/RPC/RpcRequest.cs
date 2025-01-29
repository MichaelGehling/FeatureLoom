using FeatureLoom.Serialization;

namespace FeatureLoom.RPC
{
    public readonly struct RpcRequest<P, R> : IRpcRequest
    {
        public readonly string method;
        public readonly long requestId;
        public readonly bool noResponse;
        public readonly P parameterSet;

        public RpcRequest(long requestId, string method, P parameterSet, bool noResponse = false)
        {
            this.requestId = requestId;
            this.method = method;
            this.parameterSet = parameterSet;
            this.noResponse = noResponse;
        }

        [JsonIgnore]
        public long RequestId => requestId;

        [JsonIgnore]
        public string Method => method;
    }
}