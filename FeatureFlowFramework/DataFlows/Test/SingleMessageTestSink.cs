using FeatureFlowFramework.Helper;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows.Test
{
    public class SingleMessageTestSink<T> : IDataFlowSink
    {
        AsyncManualResetEvent receivedEvent = new AsyncManualResetEvent();
        public T receivedMessage;
        public bool received = false;

        public void Post<M>(in M message)
        {
            if (message is T msgT)
            {
                receivedMessage = msgT;
                received = true;
                receivedEvent.Set();
            }
        }

        public Task PostAsync<M>(M message)
        {
            if (message is T msgT)
            {
                receivedMessage = msgT;
                received = true;
                receivedEvent.Set();
            }
            return Task.CompletedTask;
        }

        public void Reset()
        {
            receivedEvent.Reset();
            received = false;
            receivedMessage = default;                        
        }

        public IAsyncWaitHandle WaitHandle => receivedEvent.AsyncWaitHandle;
    }
}
