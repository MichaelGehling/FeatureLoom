using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureFlowFramework.DataFlows.RPC
{
    public class QueuingRpcCallee : RpcCallee
    {
        QueueReceiver<object> queue = new QueueReceiver<object>();

        public override void Post<M>(in M message)
        {
            if (message is IRpcRequest || message is string)
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
    }
}
