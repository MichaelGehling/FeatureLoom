using FeatureLoom.MessageFlow;
using FeatureLoom.Time;
using System;

namespace FeatureLoom.RPC
{
    public partial class RpcCaller
    {
        private class MultiResponseHandler<R> : IResponseHandler
        {
            private readonly long requestId;
            private readonly IMessageSink sink;
            public readonly TimeFrame lifeTime;

            public TimeFrame LifeTime => lifeTime;

            public MultiResponseHandler(long requestId, IMessageSink sink, TimeSpan timeout)
            {
                this.sink = sink;
                lifeTime = new TimeFrame(timeout);
                this.requestId = requestId;
            }

            public bool Handle<M>(in M message)
            {
                if (message is RpcResponse<R> myResponse && myResponse.RequestId == this.requestId)
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