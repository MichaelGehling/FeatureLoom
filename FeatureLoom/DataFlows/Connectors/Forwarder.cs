using System;
using System.Threading.Tasks;

namespace FeatureLoom.DataFlows
{
    /// <summary>
    ///     Just forwards messages without processing it. It is thread-safe.
    /// </summary>
    public class Forwarder : IDataFlowSource, IDataFlowConnection
    {
        protected SourceValueHelper sourceHelper;

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

        public virtual void Post<M>(in M message)
        {
            sourceHelper.Forward(in message);
        }

        public virtual void Post<M>(M message)
        {
            sourceHelper.Forward(message);
        }

        public virtual Task PostAsync<M>(M message)
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


    /// <summary>
    ///     Forwards messages if they are of the defined type without processing it. It is thread-safe.
    /// </summary>
    public class Forwarder<T> : IDataFlowConnection<T>
    {
        protected TypedSourceValueHelper<T> sourceHelper;

        public Type SentMessageType => typeof(T);
        public Type ConsumedMessageType => typeof(T);

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        public void ConnectTo(IDataFlowSink sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink, bool weakReference = false)
        {
            return sourceHelper.ConnectTo(sink, weakReference);
        }

        public void DisconnectAll()
        {
            sourceHelper.DisconnectAll();
        }

        public void DisconnectFrom(IDataFlowSink sink)
        {
            sourceHelper.DisconnectFrom(sink);
        }

        public IDataFlowSink[] GetConnectedSinks()
        {
            return sourceHelper.GetConnectedSinks();
        }

        public void Post<M>(in M message)
        {
            if (message is T) sourceHelper.Forward(in message);
        }

        public void Post<M>(M message)
        {
            if (message is T) sourceHelper.Forward(message);
        }

        public Task PostAsync<M>(M message)
        {
            if (message is T) return sourceHelper.ForwardAsync(message);
            else return Task.CompletedTask;
        }
    }
}