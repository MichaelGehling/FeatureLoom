using FeatureLoom.Helpers;
using FeatureLoom.Helpers.Time;
using System;

namespace FeatureLoom.DataFlows.RPC
{
    public partial class RpcCaller
    {
        private class MultiResponseHandler<R> : IResponseHandler
        {
            private readonly long requestId;
            private readonly IDataFlowSink sink;
            public readonly TimeFrame lifeTime;

            public TimeFrame LifeTime => lifeTime;

            public MultiResponseHandler(long requestId, IDataFlowSink sink, TimeSpan timeout)
            {
                this.sink = sink;
                lifeTime = new TimeFrame(timeout);
                this.requestId = requestId;
            }

            public bool Handle<M>(in M message)
            {
                if(message is RpcResponse<R> myResponse && myResponse.RequestId == this.requestId)
                {
                    sink.Post(myResponse.Result);
                }

                return false;
            }

            public void Cancel()
            {
                // do nothing
            }
        }
    }
}