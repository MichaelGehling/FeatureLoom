using FeatureFlowFramework.Logging;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows
{
    public class ReplyingEndpoint<REQ, REP> : IReplier, IAlternativeDataFlow
    {
        private readonly Func<REQ, (REP replyMsg, bool success)> reply;
        private DataFlowSourceHelper sendingHelper = new DataFlowSourceHelper();
        private DataFlowSourceHelper alternativeSendingHelper = null;

        public int CountConnectedSinks => sendingHelper.CountConnectedSinks;

        public IDataFlowSink[] GetConnectedSinks()
        {
            return sendingHelper.GetConnectedSinks();
        }

        public ReplyingEndpoint(Func<REQ, (REP replyMsg, bool success)> reply)
        {
            // TODO What if postAsync is called?
            this.reply = WrapInTryCatchIfAsync(reply);
        }

        public IDataFlowSource Else
        {
            get
            {
                if(alternativeSendingHelper == null) alternativeSendingHelper = new DataFlowSourceHelper();
                return alternativeSendingHelper;
            }
        }

        public void ConnectTo(IDataFlowSink sink)
        {
            ((IDataFlowSource)sendingHelper).ConnectTo(sink);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink)
        {
            return ((IDataFlowSource)sendingHelper).ConnectTo(sink);
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
            if(message is IRequest reqWrapper && reqWrapper.TryGetMessage<REQ>(out REQ requestMessage))
            {
                var (replyMsg, success) = reply(requestMessage);
                if(success) sendingHelper.Forward(reqWrapper.CreateReply(replyMsg));
                else alternativeSendingHelper?.Forward(message);
            }
            else alternativeSendingHelper?.Forward(message);
        }

        public async Task PostAsync<M>(M message)
        {
            if(message is IRequest reqWrapper && reqWrapper.TryGetMessage<REQ>(out REQ requestMessage))
            {
                var (replyMsg, success) = reply(requestMessage);
                if(success) await sendingHelper.ForwardAsync(reqWrapper.CreateReply(replyMsg));
                else await alternativeSendingHelper?.ForwardAsync(message);
            }
            else await alternativeSendingHelper?.ForwardAsync(message);
        }

        private Func<T, (R, bool)> WrapInTryCatchIfAsync<T, R>(Func<T, (R, bool)> function, bool logOnException = true)
        {
            Func<T, (R, bool)> result = function;
            if(function.Method.GetCustomAttribute(typeof(AsyncStateMachineAttribute)) != null)
            {
                result = t =>
                {
                    try
                    {
                        return function(t);
                    }
                    catch(Exception e)
                    {
                        if(logOnException) Log.ERROR($"Async function in ReplyingEndpoint failed with an exception that was caught! ", e.ToString());
                        return (default, false);
                    }
                };
            }
            return result;
        }
    }
}
