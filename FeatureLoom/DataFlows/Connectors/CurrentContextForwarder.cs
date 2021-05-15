using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.DataFlows
{
    /// <summary>
    /// Assures that incoming messages are forwarded within the synchronization context where this CurrentContextForwarder is instantiated.
    /// This can be used, e.g. to process a message in a ProcessingEndpoint within a UI-Thread, though the message was send from some other thread!
    /// Note: structs will be boxed. If you only have one message type, you can use the typed CurrentContextForwarder<T> to avoid boxing.
    /// </summary>
    public class CurrentContextForwarder : CurrentContextForwarder<object>
    {
    }

    /// <summary>
    /// Assures that incoming messages are forwarded within the synchronization context where this CurrentContextForwarder is instantiated.
    /// This can be used, e.g. to process a message in a ProcessingEndpoint within a UI-Thread, though the message was send from some other thread!
    /// </summary>
    public class CurrentContextForwarder<T> : Forwarder, IDataFlowConnection<T>
    {
        private readonly QueueReceiver<T> receiver;
        private CancellationTokenSource cts = new CancellationTokenSource();
        private Task forwardingTask;

        public Type SentMessageType => typeof(T);
        public Type ConsumedMessageType => typeof(T);

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
            receiver.Post(in message);
        }

        public override void Post<M>(M message)
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
            while (!cancellationToken.IsCancellationRequested)
            {
                await receiver.WaitAsync();
                while (receiver.TryReceive(out T message))
                {
                    try
                    {
                        base.Post(message);
                    }
                    catch (Exception e)
                    {
                        Log.ERROR(this.GetHandle(), "Exception caught in CurrentContextForwarder while sending.", e.ToString());
                    }
                }
            }
        }
    }
}