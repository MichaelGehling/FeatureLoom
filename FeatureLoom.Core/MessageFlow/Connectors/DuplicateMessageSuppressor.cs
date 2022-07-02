using FeatureLoom.Extensions;
using FeatureLoom.Scheduling;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    public class DuplicateMessageSuppressor : IMessageSource, IMessageFlowConnection
    {
        private SourceValueHelper sourceHelper;
        private Queue<(object message, DateTime suppressionEnd)> suppressors = new Queue<(object, DateTime)>();
        private MicroLock suppressorsLock = new MicroLock();
        private readonly TimeSpan suppressionTime;
        private readonly TimeSpan cleanupPeriode = 10.Seconds();
        private readonly Func<object, object, bool> isDuplicate;
        private DateTime nextCleanUp;
        private TimeSpan cleanupTolerance = 1.Seconds();
        private ISchedule scheduledAction;

        public DuplicateMessageSuppressor(TimeSpan suppressionTime, Func<object, object, bool> isDuplicate = null, TimeSpan cleanupPeriode = default, TimeSpan cleanupTolerance = default)
        {
            this.suppressionTime = suppressionTime;
            if (isDuplicate == null) isDuplicate = (a, b) => a.Equals(b);
            this.isDuplicate = isDuplicate;
            if (cleanupPeriode == default) cleanupPeriode = this.cleanupPeriode;
            cleanupPeriode = cleanupPeriode.Clamp(suppressionTime.Multiply(100), TimeSpan.MaxValue);
            this.cleanupPeriode = cleanupPeriode;
            this.nextCleanUp = AppTime.Now + this.cleanupPeriode;        
            if (this.cleanupTolerance != default) this.cleanupTolerance = cleanupTolerance;

            this.scheduledAction = Scheduler.ScheduleAction("DuplicateMessageSuppressor", now => 
            {
                TimeFrame nextTriggerTimeFrame;
                if (now > nextCleanUp - this.cleanupTolerance)
                {
                    using (suppressorsLock.Lock())
                    {
                        CleanUpSuppressors(now);
                    }
                    nextTriggerTimeFrame = new TimeFrame(now, this.cleanupPeriode);
                }
                else
                {
                    nextTriggerTimeFrame = new TimeFrame (now, nextCleanUp);
                }

                return nextTriggerTimeFrame;
            });
        }

        public void AddSuppressor<M>(M suppressorMessage)
        {
            using (suppressorsLock.Lock())
            {
                DateTime now = AppTime.Now;
                suppressors.Enqueue((suppressorMessage, now + suppressionTime));
            }
        }

        private bool IsSuppressed<M>(M message)
        {
            using (suppressorsLock.Lock())
            {
                DateTime now = AppTime.Now;
                CleanUpSuppressors(now);
                foreach (var suppressor in suppressors)
                {
                    if (isDuplicate(suppressor.message, message))
                    {
                        return true;
                    }
                }
                suppressors.Enqueue((message, now + suppressionTime));
            }
            return false;
        }

        private void CleanUpSuppressors(DateTime now)
        {
            nextCleanUp = now + cleanupPeriode;
            while (suppressors.Count > 0)
            {
                if (now > suppressors.Peek().suppressionEnd) suppressors.Dequeue();
                else break;
            }
        }

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

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

        public void Post<M>(in M message)
        {
            if (!IsSuppressed(message)) sourceHelper.Forward(in message);
        }

        public void Post<M>(M message)
        {
            if (!IsSuppressed(message)) sourceHelper.Forward(message);
        }

        public Task PostAsync<M>(M message)
        {
            if (IsSuppressed(message)) return Task.CompletedTask;
            else return sourceHelper.ForwardAsync(message);
        }

        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
        }

        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
        {
            return sourceHelper.ConnectTo(sink, weakReference);
        }
    }
}