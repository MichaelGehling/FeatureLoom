using FeatureLoom.Helpers;
using FeatureLoom.Helpers.Extensions;
using FeatureLoom.Helpers.Time;
using System;
using System.Threading.Tasks;

namespace FeatureLoom.DataFlows.RPC
{
    public partial class StringRpcCaller
    {
        private interface IResponseHandler
        {
            bool Handle<M>(in M message);

            TimeFrame LifeTime { get; }

            void Cancel();
        }

        private class ResponseHandler : IResponseHandler
        {
            private readonly long requestId;
            private readonly TaskCompletionSource<string> taskCompletionSource;
            public readonly TimeFrame lifeTime;

            public TimeFrame LifeTime => lifeTime;

            public ResponseHandler(long requestId, TaskCompletionSource<string> taskCompletionSource, TimeSpan timeout)
            {
                this.taskCompletionSource = taskCompletionSource;
                lifeTime = new TimeFrame(timeout);
                this.requestId = requestId;
            }

            public bool Handle<M>(in M message)
            {
                if(message is IRpcResponse myResponse && myResponse.RequestId == this.requestId)
                {
                    taskCompletionSource.SetResult(myResponse.ResultToJson().Trim('"'.ToSingleEntryArray()));
                    return true;
                }
                else return false;
            }

            public void Cancel()
            {
                taskCompletionSource.SetCanceled();
            }
        }
    }
}