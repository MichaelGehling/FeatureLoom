using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using System;
using System.Collections;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    /// <summary>
    /// Feeds incoming messages into an AggregationData object.
    /// When the aggregation data is complete it will send one or multiple messages to connected sinks.
    /// </summary>
    /// <typeparam name="I">The input type. If multiple message types are consumed for aggregation, this must be a common supertype</typeparam>
    /// <typeparam name="O">The output type. If multiple message types are generated from the aggregated data, this must be a common supertype</typeparam>
    public class Aggregator<I, O> : IMessageFlowConnection<I, O>, IAlternativeMessageSource
    {
        private SourceValueHelper sourceHelper = new SourceValueHelper();
        private LazyValue<SourceHelper> alternativeSender;

        private IAggregationData aggregationData;
        private FeatureLock dataLock = new FeatureLock();

        public Aggregator(IAggregationData aggregationData)
        {
            this.aggregationData = aggregationData;
        }

        public Type ConsumedMessageType => typeof(I);


        public void Post<M>(in M message)
        {
            bool alternative = true;
            if (message is I typedMessage)
            {
                using (dataLock.Lock())
                {
                    alternative = !aggregationData.AddMessage(typedMessage);
                    while (aggregationData.TryGetAggregatedMessage(out O aggregatedMessage))
                    {
                        if (aggregationData.ForwardByRef) sourceHelper.Forward(in aggregatedMessage);
                        else sourceHelper.Forward(aggregatedMessage);
                    }
                }
            }
            if (alternative) alternativeSender.ObjIfExists?.Forward(in message);
        }

        public void Post<M>(M message)
        {
            bool alternative = true;
            if (message is I typedMessage)
            {
                using (dataLock.Lock())
                {
                    alternative = !aggregationData.AddMessage(typedMessage);
                    while (aggregationData.TryGetAggregatedMessage(out O aggregatedMessage))
                    {
                        if (aggregationData.ForwardByRef) sourceHelper.Forward(in aggregatedMessage);
                        else sourceHelper.Forward(aggregatedMessage);
                    }
                }
            }
            if (alternative) alternativeSender.ObjIfExists?.Forward(message);
        }

        public Task PostAsync<M>(M message)
        {
            bool alternative = true;
            if (message is I typedMessage)
            {
                using (dataLock.Lock())
                {
                    alternative = !aggregationData.AddMessage(typedMessage);
                    while (aggregationData.TryGetAggregatedMessage(out O aggregatedMessage))
                    {
                        sourceHelper.ForwardAsync(aggregatedMessage);
                    }
                }
            }            
            if (alternative) return alternativeSender.ObjIfExists?.ForwardAsync(message);
            return Task.CompletedTask;
        }

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        public IMessageSource Else => alternativeSender.Obj;

        public Type SentMessageType => typeof(O);

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

        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
        }

        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
        {
            return sourceHelper.ConnectTo(sink, weakReference);
        }

        public interface IAggregationData
        {
            bool AddMessage(I message);
            bool TryGetAggregatedMessage(out O aggregatedMessage);
            bool ForwardByRef { get; }
        }
    }

    
}