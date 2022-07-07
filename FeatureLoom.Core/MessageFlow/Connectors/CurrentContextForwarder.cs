using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
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
    public class CurrentContextForwarder<T> : IMessageFlowConnection<T>
    {        
        readonly struct ForwardingMessage
        {
            public readonly T message;
            public readonly ForwardingMethod forwardingMethod;

            public ForwardingMessage(T message, ForwardingMethod forwardingMethod)
            {
                this.message = message;
                this.forwardingMethod = forwardingMethod;
            }
        }

        private TypedSourceValueHelper<T> sourceHelper = new TypedSourceValueHelper<T>();
        private readonly QueueReceiver<ForwardingMessage> receiver;
        private CancellationTokenSource cts = new CancellationTokenSource();
        private Task forwardingTask;

        public Type SentMessageType => typeof(T);
        public Type ConsumedMessageType => typeof(T);

        public CurrentContextForwarder()
        {
            receiver = new QueueReceiver<ForwardingMessage>();
            forwardingTask = RunAsync(cts.Token);
        }

        public void Cancel() => cts.Cancel();

        public bool IsCancelled => cts.IsCancellationRequested;

        public int Count => receiver.Count;

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        public void Post<M>(in M message)
        {
            if (message is T typedMessage)
            {
                ForwardingMessage forwardingMessage = new ForwardingMessage(typedMessage, ForwardingMethod.SynchronousByRef);
                receiver.Post(in forwardingMessage);
            }
        }

        public void Post<M>(M message)
        {
            if (message is T typedMessage)
            {
                ForwardingMessage forwardingMessage = new ForwardingMessage(typedMessage, ForwardingMethod.Synchronous);
                receiver.Post(forwardingMessage);
            }
        }

        public Task PostAsync<M>(M message)
        {
            if (message is T typedMessage)
            {
                ForwardingMessage forwardingMessage = new ForwardingMessage(typedMessage, ForwardingMethod.Asynchronous);
                receiver.Post(forwardingMessage);                
            }
            return Task.CompletedTask;
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await receiver.WaitAsync();
                while (receiver.TryReceive(out ForwardingMessage forwardingMessage))
                {
                    try
                    {
                        if (forwardingMessage.forwardingMethod == ForwardingMethod.Synchronous) sourceHelper.Forward(forwardingMessage.message);
                        else if (forwardingMessage.forwardingMethod == ForwardingMethod.SynchronousByRef) sourceHelper.Forward(in forwardingMessage.message);
                        else if (forwardingMessage.forwardingMethod == ForwardingMethod.Asynchronous) await sourceHelper.ForwardAsync(forwardingMessage.message);
                    }
                    catch (Exception e)
                    {
                        Log.ERROR(this.GetHandle(), "Exception caught in CurrentContextForwarder while sending.", e.ToString());
                    }
                }
            }
        }

        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
        }

        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
        {
            return sourceHelper.ConnectTo(sink, weakReference);
        }

        public void DisconnectFrom(IMessageSink sink)
        {
            sourceHelper.DisconnectFrom(sink);
        }

        public void DisconnectAll()
        {
            sourceHelper.DisconnectAll();
        }

        public IMessageSink[] GetConnectedSinks()
        {
            return sourceHelper.GetConnectedSinks();
        }
    }
}