using Newtonsoft.Json;

namespace FeatureLoom.DataFlows.RPC
{
    public struct RpcRequest<P, R> : IRpcRequest
    {
        public string method;
        public long requestId;
        public bool noResponse;
        public P parameterSet;

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