using System;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    public interface IMessageFlow { }

    public interface IMessageSink : IMessageFlow
    {
        void Post<M>(in M message);

        void Post<M>(M message);

        Task PostAsync<M>(M message);
    }

    public interface IMessageSource : IMessageFlow
    {
        void ConnectTo(IMessageSink sink, bool weakReference = false);

        IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false);

        void DisconnectFrom(IMessageSink sink);

        void DisconnectAll();

        int CountConnectedSinks { get; }

        IMessageSink[] GetConnectedSinks();

        bool IsConnected(IMessageSink sink);
    }


    public interface ITypedMessageSink : IMessageSink
    {
        Type ConsumedMessageType { get; }
    }

    public interface IMessageSink<T> : ITypedMessageSink
    {
    }

    public interface ITypedMessageSource : IMessageSource
    {
        Type SentMessageType { get; }
    }

    public interface IMessageSource<T> : ITypedMessageSource
    {
    }

    public interface IMessageFlowConnection : IMessageSink, IMessageSource
    {
    }

    public interface IMessageFlowConnection<T> : IMessageFlowConnection, IMessageSink<T>, IMessageSource<T>
    {
    }

    public interface IMessageFlowConnection<I, O> : IMessageFlowConnection, IMessageSink<I>, IMessageSource<O>
    {
    }

    public interface IAlternativeMessageSource
    {
        IMessageSource Else { get; }
    }

    public interface IReplier : IMessageSource, IMessageSink
    {
    };

    public interface IRequester : IMessageSource, IMessageSink
    {
        void ConnectToAndBack(IReplier replier, bool weakReference = false);
    };

    public interface IMessageQueue : IMessageSink
    {
        int Count { get; }

        object[] GetQueuedMesssages();
    }

    public interface IRequestMessage<T>
    {
        public long RequestId { get; set; }
        public T Content { get; }
    }

    public interface IResponseMessage<T>
    {
        public long RequestId { get; set; }
        public T Content { get; }
    }
}