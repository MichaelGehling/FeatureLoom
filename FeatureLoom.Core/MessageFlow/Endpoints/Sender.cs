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

        public void Send<T>(T message)
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

        public bool IsConnected(IMessageSink sink)
        {
            return sourceHelper.IsConnected(sink);
        }
    }

    /// <summary> Used to send messages of a specific type to all connected sinks. It is thread safe. <summary>
    public class Sender<T> : ISender<T>, IMessageSource<T>
    {
        protected TypedSourceValueHelper<T> sourceHelper;

        public Type SentMessageType => sourceHelper.SentMessageType;

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

    }
}