using System;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows
{
    /// <summary>
    ///     Messages from a dataFlow source put to the filter are checked in a filter function and
    ///     forwarded to connected sinks if passing the filter. Messages that doesn't match the
    ///     input type are filtered out, too. By omitting the filter function, it is possible to
    ///     filter only on the message's type. It is thread-safe as long as the provided function is
    ///     also thread-safe. Avoid long running functions to avoid blocking the sender
    /// </summary>
    /// <typeparam name="T"> The input type for the filter function </typeparam>
    public class Filter<T> : IDataFlowSource, IDataFlowConnection, IAlternativeDataFlow
    {
        protected DataFlowSourceHelper sendingHelper = new DataFlowSourceHelper();
        protected Func<T, bool> predicate;
        protected DataFlowSourceHelper alternativeSendingHelper = null;

        public int CountConnectedSinks => sendingHelper.CountConnectedSinks;

        public IDataFlowSink[] GetConnectedSinks()
        {
            return sendingHelper.GetConnectedSinks();
        }

        public Filter(Func<T, bool> predicate = null)
        {
            this.predicate = predicate;
        }

        public IDataFlowSource Else
        {
            get
            {
                if(alternativeSendingHelper == null) alternativeSendingHelper = new DataFlowSourceHelper();
                return alternativeSendingHelper;
            }
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
            if(message is T msgT)
            {
                if(predicate == null || predicate(msgT)) sendingHelper.Forward(msgT);
                else alternativeSendingHelper?.Forward(msgT);
            }
            else alternativeSendingHelper?.Forward(message);
        }

        public Task PostAsync<M>(M message)
        {
            if(message is T msgT)
            {
                if(predicate == null || predicate(msgT)) return sendingHelper.ForwardAsync(msgT);
                else return alternativeSendingHelper?.ForwardAsync(msgT);
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