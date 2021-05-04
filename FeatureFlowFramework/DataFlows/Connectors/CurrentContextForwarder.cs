using FeatureLoom.Services.Logging;
using FeatureLoom.Services.MetaData;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.DataFlows
{
    public class CurrentContextForwarder : CurrentContextForwarder<object>
    {
    }
    public class CurrentContextForwarder<T> : Forwarder, IDataFlowConnection<T>
    {
        private readonly QueueReceiver<T> receiver;
        CancellationTokenSource cts = new CancellationTokenSource();
        Task forwardingTask;

        public CurrentContextForwarder()
        {
            receiver = new QueueReceiver<T>();
            forwardingTask = RunAsync(cts.Token);
        }

        public void Cancel() => cts.Cancel();

        public bool IsCancelled => cts.IsCancellationRequested;

        public int Count => receiver.Count;

        public override void Post<M>(in M message)
        {
            receiver.Post(message);            
        }

        public override Task PostAsync<M>(M message)
        {
            Task task = receiver.PostAsync(message);
            return task;
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            while(!cancellationToken.IsCancellationRequested)
            {
                await receiver.WaitAsync();
                while(receiver.TryReceive(out T message))
                {
                    try
                    {
                        base.Post(message);
                    }
                    catch(Exception e)
                    {
                        Log.ERROR(this.GetHandle(), "Exception caught in CurrentContextForwarder while sending.", e.ToString());
                    }
                }                
            }
        }
    }
}