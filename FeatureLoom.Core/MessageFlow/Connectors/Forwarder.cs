using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    /// <summary>
    ///     Just forwards messages without processing it. It is thread-safe.
    /// </summary>
    public sealed class Forwarder : IMessageFlowConnection
    {
        private SourceValueHelper sourceHelper;

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        public bool NoConnectedSinks => sourceHelper.NotConnected;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IMessageSink[] GetConnectedSinks()
        {
            return sourceHelper.GetConnectedSinks();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DisconnectAll()
        {
            sourceHelper.DisconnectAll();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DisconnectFrom(IMessageSink sink)
        {
            sourceHelper.DisconnectFrom(sink);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Post<M>(in M message)
        {
            sourceHelper.Forward(in message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Post<M>(M message)
        {
            sourceHelper.Forward(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task PostAsync<M>(M message)
        {
            return sourceHelper.ForwardAsync(message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
        {
            return sourceHelper.ConnectTo(sink, weakReference);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        private TypedSourceValueHelper<T> sourceHelper;

        public Type SentMessageType => typeof(T);
        public Type ConsumedMessageType => typeof(T);

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        public bool NoConnectedSinks => sourceHelper.NotConnected;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
        {
            return sourceHelper.ConnectTo(sink, weakReference);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DisconnectAll()
        {
            sourceHelper.DisconnectAll();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DisconnectFrom(IMessageSink sink)
        {
            sourceHelper.DisconnectFrom(sink);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IMessageSink[] GetConnectedSinks()
        {
            return sourceHelper.GetConnectedSinks();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsConnected(IMessageSink sink)
        {
            return sourceHelper.IsConnected(sink);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Post<M>(in M message)
        {
            if (message is T typedMessage) sourceHelper.Forward(in typedMessage);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Post<M>(M message)
        {
            if (message is T typedMessage) sourceHelper.Forward(typedMessage);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task PostAsync<M>(M message)
        {
            if (message is T typedMessage) return sourceHelper.ForwardAsync(typedMessage);
            else return Task.CompletedTask;
        }
    }
}