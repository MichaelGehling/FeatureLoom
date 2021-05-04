using System;
using System.Threading.Tasks;

namespace FeatureLoom.DataFlows
{
    /// <summary>
    ///     Messages from a dataFlow put to the converter are processed in a converter function and
    ///     forwarded to connected sinks. Messages that doesn't match the input type are forwarded as they are. It
    ///     is thread-safe as long as the provided function is also thread-safe. Avoid long running
    ///     functions to avoid blocking the sender
    /// </summary>
    /// <typeparam name="I"> The input type for the converter function </typeparam>
    /// <typeparam name="O"> The output type for the converter function </typeparam>
    public class MessageConverter<I, O> : IDataFlowConnection<I, O>
    {
        private SourceValueHelper sourceHelper = new SourceValueHelper();
        private readonly Func<I, O> convertFunc;

        public MessageConverter(Func<I, O> convertFunc)
        {
            this.convertFunc = convertFunc;
        }


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

        public void Post<M>(in M message)
        {
            if(message is I msgT)
            {
                sourceHelper.Forward(convertFunc(msgT));
            }
            else sourceHelper.Forward(message);
        }

        public Task PostAsync<M>(M message)
        {
            if(message is I msgT)
            {
                return sourceHelper.ForwardAsync(convertFunc(msgT));
            }
            else return sourceHelper.ForwardAsync(message);
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
}