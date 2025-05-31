using FeatureLoom.Extensions;
using FeatureLoom.Scheduling;
using FeatureLoom.DependencyInversion;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    public sealed class DuplicateMessageSuppressor : IMessageSource, IMessageFlowConnection
    {
        private SourceValueHelper sourceHelper;
        private Queue<(object message, DateTime suppressionEnd)> suppressors = new Queue<(object, DateTime)>();
        private MicroLock suppressorsLock = new MicroLock();
        private readonly TimeSpan suppressionTime;
        private readonly TimeSpan cleanupPeriode = 10.Seconds();
        private readonly Func<object, object, bool> isDuplicate;
        private ISchedule scheduledAction;

        public DuplicateMessageSuppressor(TimeSpan suppressionTime, Func<object, object, bool> isDuplicate = null, TimeSpan cleanupPeriode = default)
        {
            this.suppressionTime = suppressionTime;
            if (isDuplicate == null) isDuplicate = (a, b) => a.Equals(b);
            this.isDuplicate = isDuplicate;

            if (cleanupPeriode == default) cleanupPeriode = this.cleanupPeriode;
            cleanupPeriode = cleanupPeriode.Clamp(suppressionTime.Multiply(100), TimeSpan.MaxValue);
            this.cleanupPeriode = cleanupPeriode;

            Action<DateTime> cleanUpAction = now =>
            {
                using (suppressorsLock.Lock())
                {
                    CleanUpSuppressors(now);
                }
            };
            this.scheduledAction = cleanUpAction.ScheduleForRecurringExecution("DuplicateMessageSuppressor", cleanupPeriode);
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

        public bool IsConnected(IMessageSink sink)
        {
            return sourceHelper.IsConnected(sink);
        }
    }
}