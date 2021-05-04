﻿using FeatureLoom.Helpers;
using FeatureLoom.Helpers.Extensions;
using Newtonsoft.Json;

namespace FeatureLoom.DataFlows.RPC
{
    public struct RpcResponse<R> : IRpcResponse
    {
        public long requestId;
        public R result;

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