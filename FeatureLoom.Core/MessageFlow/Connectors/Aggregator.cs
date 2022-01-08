using FeatureLoom.Helpers;
using FeatureLoom.Scheduling;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
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

        private TimeFrame nextTimeoutCheck;
        private bool waitingForTimeout = false;
        private TimeSpan maxSupervisionTimeout = TimeSpan.MaxValue;
        private IAggregationData aggregationData;
        private FeatureLock dataLock = new FeatureLock();

        public Aggregator(IAggregationData aggregationData, TimeSpan supervisionCycleTime = default)
        {
            this.aggregationData = aggregationData;

            if (supervisionCycleTime != default && supervisionCycleTime != TimeSpan.MaxValue)
            {
                PrepareSupervision(aggregationData, supervisionCycleTime);
            }
        }

        private void PrepareSupervision(IAggregationData aggregationData, TimeSpan supervisionCycleTime)
        {
            this.maxSupervisionTimeout = supervisionCycleTime;
            WeakReference<Aggregator<I, O>> weakRef = new WeakReference<Aggregator<I, O>>(this);
            Scheduler.ScheduleAction(now =>
            {
                if (!weakRef.TryGetTarget(out var me)) return (false, default);
                if (!me.waitingForTimeout) return (true, me.maxSupervisionTimeout);
                if (!me.nextTimeoutCheck.Elapsed(now)) return (true, me.maxSupervisionTimeout);

                if (me.dataLock.TryLock(out var lockHandle))
                {
                    using (lockHandle)
                    {
                        while (me.aggregationData.TryGetAggregatedMessage(true, out O aggregatedMessage))
                        {
                            me.sourceHelper.Forward(aggregatedMessage);
                        }
                        me.waitingForTimeout = me.aggregationData.TryGetTimeout(out var timeout);
                        if (me.waitingForTimeout) me.nextTimeoutCheck = new TimeFrame(timeout);
                    }
                }
                return (true, me.maxSupervisionTimeout);
            });
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
                    while (aggregationData.TryGetAggregatedMessage(false, out O aggregatedMessage))
                    {
                        if (aggregationData.ForwardByRef) sourceHelper.Forward(in aggregatedMessage);
                        else sourceHelper.Forward(aggregatedMessage);
                    }
                    SetTimeout();
                }
            }
            if (alternative) alternativeSender.ObjIfExists?.Forward(in message);
        }

        private void SetTimeout()
        {
            waitingForTimeout = aggregationData.TryGetTimeout(out var timeout);
            if (waitingForTimeout) nextTimeoutCheck = new TimeFrame(timeout);
        }

        public void Post<M>(M message)
        {
            bool alternative = true;
            if (message is I typedMessage)
            {
                using (dataLock.Lock())
                {
                    alternative = !aggregationData.AddMessage(typedMessage);
                    while (aggregationData.TryGetAggregatedMessage(false, out O aggregatedMessage))
                    {
                        if (aggregationData.ForwardByRef) sourceHelper.Forward(in aggregatedMessage);
                        else sourceHelper.Forward(aggregatedMessage);
                    }
                    SetTimeout();
                }
            }
            if (alternative) alternativeSender.ObjIfExists?.Forward(message);
        }

        public async Task PostAsync<M>(M message)
        {
            bool alternative = true;
            if (message is I typedMessage)
            {
                using (await dataLock.LockAsync())
                {
                    alternative = !aggregationData.AddMessage(typedMessage);
                    while (aggregationData.TryGetAggregatedMessage(false, out O aggregatedMessage))
                    {
                        await sourceHelper.ForwardAsync(aggregatedMessage);
                    }
                    SetTimeout();
                }
            }            
            if (alternative) await alternativeSender.ObjIfExists?.ForwardAsync(message);            
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
            bool TryGetAggregatedMessage(bool timeoutCall, out O aggregatedMessage);
            bool TryGetTimeout(out TimeSpan timeout);
            bool ForwardByRef { get; }
        }
    }

    
}