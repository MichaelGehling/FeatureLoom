using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows
{
    public interface IDataFlow { }

    public interface IDataFlowSink : IDataFlow
    {
        void Post<M>(in M message);

        Task PostAsync<M>(M message);
    }

    public interface IDataFlowQueue : IDataFlowSink
    {
        int CountQueuedMessages { get; }

        object[] GetQueuedMesssages();
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

    public interface IDataFlowConnection : IDataFlowSink, IDataFlowSource
    {
    }

    public interface IAlternativeDataFlow
    {
        IDataFlowSource Else { get; }
    }

    public interface IReplier : IDataFlowSource, IDataFlowSink { };

    public interface IRequester : IDataFlowSource, IDataFlowSink
    {
        void ConnectToAndBack(IReplier replier, bool weakReference = false);
    };
}