using FeatureFlowFramework.Helper;
using System;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows.RPC
{
    public partial class RpcCaller
    {
        interface IResponseHandler
        {
            bool Handle<M>(in M message);
            TimeFrame LifeTime { get; }
            void Cancel();
        }

        class ResponseHandler<R> : IResponseHandler
        {
            readonly long requestId;
            readonly TaskCompletionSource<R> taskCompletionSource;
            public readonly TimeFrame lifeTime;

            public TimeFrame LifeTime => lifeTime;

            public ResponseHandler(long requestId, TaskCompletionSource<R> taskCompletionSource, TimeSpan timeout)
            {
                this.taskCompletionSource = taskCompletionSource;
                lifeTime = new TimeFrame(timeout);
                this.requestId = requestId;                
            }

            public bool Handle<M>(in M message)
            {
                if (message is RpcResponse<R> myResponse && myResponse.RequestId == this.requestId)
                {
                    taskCompletionSource.SetResult(myResponse.Result);
                    return true;
                }
                else return false;
            }

            public void Cancel()
            {
                taskCompletionSource.TrySetCanceled();
            }
        }
    }


}
