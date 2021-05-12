using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using System;
using System.Collections;
using System.Threading.Tasks;

namespace FeatureLoom.DataFlows
{
    
    public class Aggregator<I, O> : IDataFlowConnection<I, O>, IAlternativeDataFlow
    {
        private SourceValueHelper sourceHelper = new SourceValueHelper();
        private LazyValue<SourceHelper> alternativeSender;

        private IAggregationData<I,O> aggregationData;
        private FeatureLock dataLock = new FeatureLock();

        public Aggregator(IAggregationData<I, O> aggregationData)
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

        public IDataFlowSource Else => alternativeSender.Obj;

        public Type SentMessageType => typeof(O);

        public void DisconnectAll()
        {
            sourceHelper.DisconnectAll();
        }

        public void DisconnectFrom(IDataFlowSink sink)
        {
            sourceHelper.DisconnectFrom(sink);
        }

        public IDataFlowSink[] GetConnectedSinks()
        {
            return sourceHelper.GetConnectedSinks();
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

    public interface IAggregationData<I, O>
    {
        bool AddMessage(I message);
        bool TryGetAggregatedMessage(out O aggregatedMessage);
        bool ForwardByRef { get; }
    }
}