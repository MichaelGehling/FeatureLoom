using System;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    /// <summary>
    ///     Just forwards messages without processing it. It is thread-safe.
    /// </summary>
    public sealed class Forwarder : IMessageSource, IMessageFlowConnection
    {
        SourceValueHelper sourceHelper;

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        public IMessageSink[] GetConnectedSinks()
        {
            return sourceHelper.GetConnectedSinks();
        }

        public void DisconnectAll()
        {
            sourceHelper.DisconnectAll();
        }

        public void DisconnectFrom(IMessageSink sink)
        {
            sourceHelper.DisconnectFrom(sink);
        }

        public void Post<M>(in M message)
        {
            sourceHelper.Forward(in message);
        }

        public void Post<M>(M message)
        {
            sourceHelper.Forward(message);
        }

        public Task PostAsync<M>(M message)
        {
            return sourceHelper.ForwardAsync(message);
        }

        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
        }

        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
        {
            return sourceHelper.ConnectTo(sink, weakReference);
        }

        public bool IsConnected(IMessageSink sink)
        {
            return sourceHelper.IsConnected(sink);
        }
    }


    /// <summary>
    ///     Forwards messages if they are of the defined type without processing it. It is thread-safe.
    /// </summary>
    public sealed class Forwarder<T> : IMessageFlowConnection<T>
    {
        TypedSourceValueHelper<T> sourceHelper;

        public Type SentMessageType => typeof(T);
        public Type ConsumedMessageType => typeof(T);

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
        }

        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
        {
            return sourceHelper.ConnectTo(sink, weakReference);
        }

        public void DisconnectAll()
        {
            sourceHelper.DisconnectAll();
        }

        public void DisconnectFrom(IMessageSink sink)
        {
            sourceHelper.DisconnectFrom(sink);
        }

        public IMessageSink[] GetConnectedSinks()
        {
            return sourceHelper.GetConnectedSinks();
        }

        public bool IsConnected(IMessageSink sink)
        {
            return sourceHelper.IsConnected(sink);
        }

        public void Post<M>(in M message)
        {
            if (message is T typedMessage) sourceHelper.Forward(in typedMessage);
        }

        public void Post<M>(M message)
        {
            if (message is T typedMessage) sourceHelper.Forward(typedMessage);
        }

        public Task PostAsync<M>(M message)
        {
            if (message is T typedMessage) return sourceHelper.ForwardAsync(typedMessage);
            else return Task.CompletedTask;
        }
    }
}