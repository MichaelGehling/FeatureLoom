using System;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows
{
    /// <summary>
    ///     Messages from a dataFlow put to the converter are processed in a converter function and
    ///     forwarded to connected sinks. Messages that doesn't match the input type are ignored. It
    ///     is thread-safe as long as the provided function is also thread-safe. Avoid long running
    ///     functions to avoid blocking the sender
    /// </summary>
    /// <typeparam name="I"> The input type for the converter function </typeparam>
    /// <typeparam name="O"> The output type for the converter function </typeparam>
    public class MessageConverter<I, O> : IDataFlowSource, IDataFlowConnection, IAlternativeDataFlow
    {
        private DataFlowSourceHelper sendingHelper = new DataFlowSourceHelper();
        private readonly Func<I, O> convertFunc;
        private DataFlowSourceHelper alternativeSendingHelper = null;

        public MessageConverter(Func<I, O> convertFunc)
        {
            this.convertFunc = convertFunc;
        }

        public IDataFlowSource Else
        {
            get
            {
                if(alternativeSendingHelper == null) alternativeSendingHelper = new DataFlowSourceHelper();
                return alternativeSendingHelper;
            }
        }

        public int CountConnectedSinks => sendingHelper.CountConnectedSinks;

        public IDataFlowSink[] GetConnectedSinks()
        {
            return sendingHelper.GetConnectedSinks();
        }

        public void DisconnectAll()
        {
            ((IDataFlowSource)sendingHelper).DisconnectAll();
        }

        public void DisconnectFrom(IDataFlowSink sink)
        {
            ((IDataFlowSource)sendingHelper).DisconnectFrom(sink);
        }

        public void Post<M>(in M message)
        {
            if(message is I msgT)
            {
                sendingHelper.Forward(convertFunc(msgT));
            }
            else alternativeSendingHelper?.Forward(message);
        }

        public Task PostAsync<M>(M message)
        {
            if(message is I msgT)
            {
                return sendingHelper.ForwardAsync(convertFunc(msgT));
            }
            else return alternativeSendingHelper?.ForwardAsync(message);
        }

        public void ConnectTo(IDataFlowSink sink, bool weakReference = false)
        {
            ((IDataFlowSource)sendingHelper).ConnectTo(sink, weakReference);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink, bool weakReference = false)
        {
            return ((IDataFlowSource)sendingHelper).ConnectTo(sink, weakReference);
        }
    }
}