﻿using FeatureLoom.Helpers.Misc;
using System;
using System.Threading.Tasks;

namespace FeatureLoom.DataFlows
{
    /// <summary>
    ///     Messages from a dataFlow source put to the filter are checked in a filter function and
    ///     forwarded to connected sinks if passing the filter. Messages that doesn't match the
    ///     input type are filtered out, too. By omitting the filter function, it is possible to
    ///     filter only on the message's type. It is thread-safe as long as the provided function is
    ///     also thread-safe. Avoid long running functions to avoid blocking the sender
    /// </summary>
    /// <typeparam name="T"> The input type for the filter function </typeparam>
    public class Filter<T> : IDataFlowConnection<T>, IAlternativeDataFlow
    {
        protected SourceValueHelper sourceHelper;
        protected Func<T, bool> predicate;
        private LazyValue<SourceHelper> alternativeSendingHelper;

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        public IDataFlowSink[] GetConnectedSinks()
        {
            return sourceHelper.GetConnectedSinks();
        }

        public Filter(Func<T, bool> predicate = null)
        {
            this.predicate = predicate;
        }

        public IDataFlowSource Else => alternativeSendingHelper.Obj;        

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
            if(message is T msgT)
            {
                if(predicate == null || predicate(msgT)) sourceHelper.Forward(msgT);
                else alternativeSendingHelper.ObjIfExists?.Forward(msgT);
            }
            else alternativeSendingHelper.ObjIfExists?.Forward(message);
        }

        public Task PostAsync<M>(M message)
        {
            if(message is T msgT)
            {
                if(predicate == null || predicate(msgT)) return sourceHelper.ForwardAsync(msgT);
                else return alternativeSendingHelper.ObjIfExists?.ForwardAsync(msgT);
            }
            else return alternativeSendingHelper.ObjIfExists?.ForwardAsync(message);
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