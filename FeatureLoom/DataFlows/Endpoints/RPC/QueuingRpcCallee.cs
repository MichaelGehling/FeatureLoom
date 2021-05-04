using FeatureLoom.Helpers;
using FeatureLoom.Helpers.Synchronization;

namespace FeatureLoom.DataFlows.RPC
{
    public class QueuingRpcCallee : RpcCallee
    {
        private QueueReceiver<object> queue = new QueueReceiver<object>();

        public override void Post<M>(in M message)
        {
            if(message is IRpcRequest || message is string)
            {
                queue.Post(message);
            }
        }

        public void HandleQueuedRpcRequests()
        {
            while(queue.TryReceive(out object request))
            {
                base.Post(request);
            }
        }

        public IAsyncWaitHandle WaitHandle => queue.WaitHandle;
        public int Count => queue.Count;
    }
}