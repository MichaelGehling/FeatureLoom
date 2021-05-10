using System;
using System.Threading.Tasks;

namespace FeatureLoom.DataFlows
{
    internal interface ISender
    {
        void Send<T>(in T message);

        Task SendAsync<T>(T message);
    }

    /// <summary> Used to send messages of any type to all connected sinks. It is thread safe. <summary>
    public class Sender : IDataFlowSource, ISender
    {
        protected SourceValueHelper sourceHelper = new SourceValueHelper();

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        public IDataFlowSink[] GetConnectedSinks()
        {
            return sourceHelper.GetConnectedSinks();
        }

        public void DisconnectAll()
        {
            sourceHelper.DisconnectAll();
        }

        public void DisconnectFrom(IDataFlowSink sink)
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

        public void ConnectTo(IDataFlowSink sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink, bool weakReference = false)
        {
            return sourceHelper.ConnectTo(sink, weakReference);
        }
    }

    /// <summary> Used to send messages of a specific type to all connected sinks. It is thread safe. <summary>
    public class Sender<T> : ISender, IDataFlowSource<T>
    {
        protected SourceValueHelper sourceHelper;

        public Type SentMessageType => typeof(T);        

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        public Type MessageType => typeof(T);

        public IDataFlowSink[] GetConnectedSinks()
        {
            return sourceHelper.GetConnectedSinks();
        }

        public void DisconnectAll()
        {
            sourceHelper.DisconnectAll();
        }

        public void DisconnectFrom(IDataFlowSink sink)
        {
            sourceHelper.DisconnectFrom(sink);
        }

        public void Send(in T message)
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

        public Task SendAsync<U>(U message)
        {
            if (typeof(T).IsAssignableFrom(typeof(U))) return sourceHelper.ForwardAsync(message);
            else throw new Exception($"Sender<{typeof(T)}> doesn't allow sending messages of type {typeof(U)}.");
        }

        public void ConnectTo(IDataFlowSink sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
        }

        public void ConnectTo<U>(IDataFlowSink<U> sink, bool weakReference = false)
        {
            if (typeof(U).IsAssignableFrom(typeof(T))) sourceHelper.ConnectTo(sink, weakReference);
            else throw new Exception($"Sender<{typeof(T)}> doesn't allow connecting to sink of type {typeof(U)}.");
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
            return sink;
        }

        public IDataFlowSource ConnectTo<U>(IDataFlowConnection<U> sink, bool weakReference = false)
        {
            if (typeof(U).IsAssignableFrom(typeof(T))) sourceHelper.ConnectTo(sink, weakReference);
            else throw new Exception($"Sender<{typeof(T)}> doesn't allow connecting to sink of type {typeof(U)}.");
            return sink;
        }

        public IDataFlowSource ConnectTo<U, V>(IDataFlowConnection<U, V> sink, bool weakReference = false)
        {
            if (typeof(U).IsAssignableFrom(typeof(T))) sourceHelper.ConnectTo(sink, weakReference);
            else throw new Exception($"Sender<{typeof(T)}> doesn't allow connecting to sink of type {typeof(U)}.");
            return sink;
        }
    }
}