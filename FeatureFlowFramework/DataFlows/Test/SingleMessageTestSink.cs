using FeatureLoom.Helpers;
using FeatureLoom.Helpers.Synchronization;
using System.Threading.Tasks;

namespace FeatureLoom.DataFlows.Test
{
    public class SingleMessageTestSink<T> : IDataFlowSink
    {
        private AsyncManualResetEvent receivedEvent = new AsyncManualResetEvent();
        public T receivedMessage;
        public bool received = false;

        public void Post<M>(in M message)
        {
            if(message is T msgT)
            {
                receivedMessage = msgT;
                received = true;
                receivedEvent.Set();
            }
        }

        public Task PostAsync<M>(M message)
        {
            if(message is T msgT)
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