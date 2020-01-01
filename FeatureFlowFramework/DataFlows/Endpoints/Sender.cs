using System;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows
{
    internal interface ISender
    {
        void Send<T>(in T message);

        Task SendAsync<T>(T message);
    }

    /// <summary> Used to send messages of any type to all connected sinks. It is thread safe. <summary>
    public class Sender : IDataFlowSource, ISender
    {
        protected DataFlowSourceHelper sendingHelper = new DataFlowSourceHelper();

        public int CountConnectedSinks => sendingHelper.CountConnectedSinks;

        public IDataFlowSink[] GetConnectedSinks()
        {
            return sendingHelper.GetConnectedSinks();
        }

        public void ConnectTo(IDataFlowSink sink)
        {
            ((IDataFlowSource)sendingHelper).ConnectTo(sink);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink)
        {
            ((IDataFlowSource)sendingHelper).ConnectTo(sink);
            return sink;
        }

        public void DisconnectAll()
        {
            ((IDataFlowSource)sendingHelper).DisconnectAll();
        }

        public void DisconnectFrom(IDataFlowSink sink)
        {
            ((IDataFlowSource)sendingHelper).DisconnectFrom(sink);
        }

        public void Send<T>(in T message)
        {
            sendingHelper.Forward(message);
        }

        public Task SendAsync<T>(T message)
        {
            return sendingHelper.ForwardAsync(message);
        }
    }

    /// <summary> Used to send messages of a specific type to all connected sinks. It is thread safe. <summary>
    public class Sender<T> : IDataFlowSource, ISender
    {
        protected DataFlowSourceHelper sendingHelper = new DataFlowSourceHelper();

        public int CountConnectedSinks => sendingHelper.CountConnectedSinks;

        public IDataFlowSink[] GetConnectedSinks()
        {
            return sendingHelper.GetConnectedSinks();
        }

        public void ConnectTo(IDataFlowSink sink)
        {
            ((IDataFlowSource)sendingHelper).ConnectTo(sink);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink)
        {
            ((IDataFlowSource)sendingHelper).ConnectTo(sink);
            return sink;
        }

        public void DisconnectAll()
        {
            ((IDataFlowSource)sendingHelper).DisconnectAll();
        }

        public void DisconnectFrom(IDataFlowSink sink)
        {
            ((IDataFlowSource)sendingHelper).DisconnectFrom(sink);
        }

        public void Send(in T message)
        {
            sendingHelper.Forward(message);
        }

        public Task SendAsync(T message)
        {
            return sendingHelper.ForwardAsync(message);
        }

        public void Send<U>(in U message)
        {
            if (typeof(U) is T) sendingHelper.Forward(message);
            else throw new Exception($"Sender<{typeof(T)}> doesn't allow sending messages of type {typeof(U)}.");
        }

        public Task SendAsync<U>(U message)
        {
            if (typeof(U) is T) return sendingHelper.ForwardAsync(message);
            else throw new Exception($"Sender<{typeof(T)}> doesn't allow sending messages of type {typeof(U)}.");
        }
    }
}