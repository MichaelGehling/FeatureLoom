﻿using FeatureLoom.MessageFlow;
using FeatureLoom.Synchronization;

namespace FeatureLoom.RPC
{
    public class QueuingRpcCallee : RpcCallee
    {
        private QueueReceiver<object> queue = new QueueReceiver<object>();

        public override void Post<M>(in M message)
        {
            if (message is IRpcRequest || message is string)
            {
                queue.Post(in message);
            }
        }

        public override void Post<M>(M message)
        {
            if (message is IRpcRequest || message is string)
            {
                queue.Post(message);
            }
        }

        public void HandleQueuedRpcRequests()
        {
            while (queue.TryReceive(out object request))
            {
                base.Post(request);
            }
        }

        public bool HandleNextRpcRequest()
        {
            if (queue.TryReceive(out object request))
            {
                base.Post(request);                
            }
            return !queue.IsEmpty;
        }

        public IAsyncWaitHandle WaitHandle => queue.WaitHandle;
        public int Count => queue.Count;
    }
}