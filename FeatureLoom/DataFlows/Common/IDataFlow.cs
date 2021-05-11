using System;
using System.Threading.Tasks;

namespace FeatureLoom.DataFlows
{
    public interface IDataFlow { }

    public interface IDataFlowSink : IDataFlow
    {
        void Post<M>(in M message);

        void Post<M>(M message);

        Task PostAsync<M>(M message);
    }

    public interface IDataFlowSource : IDataFlow
    {
        void ConnectTo(IDataFlowSink sink, bool weakReference = false);

        IDataFlowSource ConnectTo(IDataFlowConnection sink, bool weakReference = false);

        void DisconnectFrom(IDataFlowSink sink);

        void DisconnectAll();

        int CountConnectedSinks { get; }

        IDataFlowSink[] GetConnectedSinks();
    }


    public interface ITypedDataFlowSink : IDataFlowSink
    {
        Type ConsumedMessageType { get; }
    }

    public interface IDataFlowSink<T> : ITypedDataFlowSink
    {
    }

    public interface ITypedDataFlowSource : IDataFlowSource
    {
        Type SentMessageType { get; }
    }

    public interface IDataFlowSource<T> : ITypedDataFlowSource
    {
    }

    public interface IDataFlowConnection : IDataFlowSink, IDataFlowSource
    {
    }

    public interface IDataFlowConnection<T> : IDataFlowConnection, IDataFlowSink<T>, IDataFlowSource<T>
    {
    }

    public interface IDataFlowConnection<I, O> : IDataFlowConnection, IDataFlowSink<I>, IDataFlowSource<O>
    {
    }

    public interface IAlternativeDataFlow
    {
        IDataFlowSource Else { get; }
    }

    public interface IReplier : IDataFlowSource, IDataFlowSink
    {
    };

    public interface IRequester : IDataFlowSource, IDataFlowSink
    {
        void ConnectToAndBack(IReplier replier, bool weakReference = false);
    };

    public interface IDataFlowQueue : IDataFlowSink
    {
        int Count { get; }

        object[] GetQueuedMesssages();
    }
}