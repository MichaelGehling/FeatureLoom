using FeatureLoom.Helpers;
using System;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    /// <summary>
    ///     Messages from a message source put to the filter are checked in a filter function and
    ///     forwarded to connected sinks if passing the filter. Messages that doesn't match the
    ///     input type are filtered out, too. By omitting the filter function, it is possible to
    ///     filter only on the message's type. It is thread-safe as long as the provided function is
    ///     also thread-safe. Avoid long running functions to avoid blocking the sender
    /// </summary>
    /// <typeparam name="T"> The input type for the filter function </typeparam>
    public sealed class Filter<T> : IMessageFlowConnection<T>, IAlternativeMessageSource
    {
        private SourceValueHelper sourceHelper;
        private Predicate<T> predicate;
        private LazyValue<SourceHelper> alternativeSendingHelper;

        public Type SentMessageType => typeof(T);
        public Type ConsumedMessageType => typeof(T);

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        public IMessageSink[] GetConnectedSinks()
        {
            return sourceHelper.GetConnectedSinks();
        }

        public Filter(Predicate<T> predicate = null)
        {
            this.predicate = predicate;
        }

        public IMessageSource Else => alternativeSendingHelper.Obj;

        public void DisconnectAll()
        {
            sourceHelper.DisconnectAll();
        }

        public void DisconnectFrom(IMessageSink sink)
        {
            sourceHelper.DisconnectFrom(sink);
        }

        public void Post<M>(in M message)
        {
            if (message is T msgT)
            {
                if (predicate == null || predicate(msgT)) sourceHelper.Forward(msgT);
                else alternativeSendingHelper.ObjIfExists?.Forward(in msgT);
            }
            else alternativeSendingHelper.ObjIfExists?.Forward(in message);
        }

        public void Post<M>(M message)
        {
            if (message is T msgT)
            {
                if (predicate == null || predicate(msgT)) sourceHelper.Forward(msgT);
                else alternativeSendingHelper.ObjIfExists?.Forward(msgT);
            }
            else alternativeSendingHelper.ObjIfExists?.Forward(message);
        }

        public Task PostAsync<M>(M message)
        {
            if (message is T msgT)
            {
                if (predicate == null || predicate(msgT)) return sourceHelper.ForwardAsync(msgT);
                else return alternativeSendingHelper.ObjIfExists?.ForwardAsync(msgT) ?? Task.CompletedTask;
            }
            else return alternativeSendingHelper.ObjIfExists?.ForwardAsync(message) ?? Task.CompletedTask;
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
}