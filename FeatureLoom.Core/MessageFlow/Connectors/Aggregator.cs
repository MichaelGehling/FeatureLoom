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
    public sealed class Aggregator<I, O> : IMessageFlowConnection<I, O>, IAlternativeMessageSource
    {
        private SourceValueHelper sourceHelper = new SourceValueHelper();
        private LazyValue<SourceHelper> alternativeSender;

        private TimeFrame nextTimeoutCheck = TimeFrame.Invalid;
        private IAggregationData aggregationData;
        private FeatureLock dataLock = new FeatureLock();
        private ISchedule scheduledAction;

        public Aggregator(IAggregationData aggregationData)
        {
            this.aggregationData = aggregationData;
        }

        private void PrepareSchedule()
        {
            this.scheduledAction = Scheduler.ScheduleAction("Aggregator", (now) =>
            {                
                if (nextTimeoutCheck.IsInvalid || !nextTimeoutCheck.Elapsed(now)) return nextTimeoutCheck;

                if (dataLock.TryLock(out var lockHandle))
                {
                    using (lockHandle)
                    {
                        while (aggregationData.TryGetAggregatedMessage(true, out O aggregatedMessage))
                        {
                            sourceHelper.Forward(aggregatedMessage);
                        }
                        bool waitingForTimeout = aggregationData.TryGetTimeout(out var timeout);
                        if (waitingForTimeout) nextTimeoutCheck = new TimeFrame(now, timeout);
                        else
                        {
                            nextTimeoutCheck = TimeFrame.Invalid;
                            this.scheduledAction = null;
                        }
                    }
                }
                return nextTimeoutCheck;
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
            bool waitingForTimeout = aggregationData.TryGetTimeout(out var timeout);
            if (waitingForTimeout)
            {
                var prevTimeoutCheck = nextTimeoutCheck;
                nextTimeoutCheck = new TimeFrame(timeout);

                if (scheduledAction == null) PrepareSchedule();
                else if (prevTimeoutCheck.IsValid && prevTimeoutCheck.utcEndTime > nextTimeoutCheck.utcEndTime) Scheduler.InterruptWaiting();                                
            }
            else
            {
                nextTimeoutCheck = TimeFrame.Invalid;
                this.scheduledAction = null;
            }
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