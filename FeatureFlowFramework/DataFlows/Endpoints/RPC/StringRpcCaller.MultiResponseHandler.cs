using FeatureFlowFramework.Helper;
using System;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows.RPC
{
    public partial class StringRpcCaller
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
                if (message is RpcResponse<R> myResponse && myResponse.RequestId == this.requestId)
                {
                    sink.Post(myResponse.ResultToJson().Trim('"'.ToSingleEntryArray()));
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