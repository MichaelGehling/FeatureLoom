using System;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{

    /// <summary> Used to send messages of any type to all connected sinks. It is thread safe. <summary>
    public class Sender : IMessageSource, ISender
    {
        protected SourceValueHelper sourceHelper = new SourceValueHelper();

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

        public void Send<T>(in T message)
        {
            sourceHelper.Forward(message);
        }

        public Task SendAsync<T>(T message)
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
    }

    /// <summary> Used to send messages of a specific type to all connected sinks. It is thread safe. <summary>
    public class Sender<T> : ISender, IMessageSource<T>
    {
        protected SourceValueHelper sourceHelper;

        public Type SentMessageType => typeof(T);        

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        public Type MessageType => typeof(T);

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

        public void Send(in T message)
        {
            sourceHelper.Forward(in message);
        }

        public void Send(T message)
        {
            sourceHelper.Forward(message);
        }

        public Task SendAsync(T message)
        {
            return sourceHelper.ForwardAsync(message);
        }

        public void Send<U>(in U message)
        {
            if (typeof(T).IsAssignableFrom(typeof(U))) sourceHelper.Forward(message);
            else throw new Exception($"Sender<{typeof(T)}> doesn't allow sending messages of type {typeof(U)}.");
        }

        public void Send<U>(U message)
        {
            if (typeof(T).IsAssignableFrom(typeof(U))) sourceHelper.Forward(in message);
            else throw new Exception($"Sender<{typeof(T)}> doesn't allow sending messages of type {typeof(U)}.");
        }

        public Task SendAsync<U>(U message)
        {
            if (typeof(T).IsAssignableFrom(typeof(U))) return sourceHelper.ForwardAsync(message);
            else throw new Exception($"Sender<{typeof(T)}> doesn't allow sending messages of type {typeof(U)}.");
        }

        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
        }

        public void ConnectTo<U>(IMessageSink<U> sink, bool weakReference = false)
        {
            if (typeof(U).IsAssignableFrom(typeof(T))) sourceHelper.ConnectTo(sink, weakReference);
            else throw new Exception($"Sender<{typeof(T)}> doesn't allow connecting to sink of type {typeof(U)}.");
        }

        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
            return sink;
        }

        public IMessageSource ConnectTo<U>(IMessageFlowConnection<U> sink, bool weakReference = false)
        {
            if (typeof(U).IsAssignableFrom(typeof(T))) sourceHelper.ConnectTo(sink, weakReference);
            else throw new Exception($"Sender<{typeof(T)}> doesn't allow connecting to sink of type {typeof(U)}.");
            return sink;
        }

        public IMessageSource ConnectTo<U, V>(IMessageFlowConnection<U, V> sink, bool weakReference = false)
        {
            if (typeof(U).IsAssignableFrom(typeof(T))) sourceHelper.ConnectTo(sink, weakReference);
            else throw new Exception($"Sender<{typeof(T)}> doesn't allow connecting to sink of type {typeof(U)}.");
            return sink;
        }
    }
}