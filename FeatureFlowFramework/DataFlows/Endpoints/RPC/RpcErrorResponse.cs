using Newtonsoft.Json;

namespace FeatureLoom.DataFlows.RPC
{
    public struct RpcErrorResponse : IRpcResponse
    {
        public long requestId;
        public string errorMessage;

        public RpcErrorResponse(long requestId, string errorMessage)
        {
            this.requestId = requestId;
            this.errorMessage = errorMessage;
        }

        [JsonIgnore]
        public long RequestId => requestId;

        [JsonIgnore]
        public string ErrorMessage => errorMessage;

        public string ResultToJson()
        {
            return errorMessage;
        }
    }
}