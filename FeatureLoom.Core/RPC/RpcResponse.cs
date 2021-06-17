using FeatureLoom.Serialization;
using Newtonsoft.Json;

namespace FeatureLoom.RPC
{
    public readonly struct RpcResponse<R> : IRpcResponse
    {
        public readonly long requestId;
        public readonly R result;

        public RpcResponse(long requestId, R result)
        {
            this.requestId = requestId;
            this.result = result;
        }

        [JsonIgnore]
        public long RequestId => requestId;

        [JsonIgnore]
        public R Result { get => result; }

        public string ResultToJson()
        {
            return result.ToJson();
        }
    }
}