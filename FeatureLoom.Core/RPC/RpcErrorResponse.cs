using Newtonsoft.Json;

namespace FeatureLoom.RPC
{
    public readonly struct RpcErrorResponse : IRpcResponse
    {
        public readonly long requestId;
        public readonly string errorMessage;

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