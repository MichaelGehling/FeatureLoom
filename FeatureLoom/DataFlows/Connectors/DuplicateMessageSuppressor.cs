using FeatureLoom.Helpers;
using FeatureLoom.Helpers.Extensions;
using FeatureLoom.Helpers.Synchronization;
using FeatureLoom.Helpers.Time;
using FeatureLoom.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.DataFlows
{
    public class DuplicateMessageSuppressor : IDataFlowSource, IDataFlowConnection
    {
        private SourceValueHelper sourceHelper;
        private Queue<(object message, DateTime suppressionEnd)> suppressors = new Queue<(object, DateTime)>();
        MicroLock suppressorsLock = new MicroLock();
        private readonly TimeSpan suppressionTime;
        private readonly TimeSpan cleanupPeriode = 10.Seconds();
        private readonly Func<object, object, bool> isDuplicate;

        public DuplicateMessageSuppressor(TimeSpan suppressionTime, Func<object, object, bool> isDuplicate = null, TimeSpan cleanupPeriode = default)
        {
            this.suppressionTime = suppressionTime;
            if(isDuplicate == null) isDuplicate = (a, b) => a.Equals(b);
            this.isDuplicate = isDuplicate;
            if(cleanupPeriode != default) this.cleanupPeriode = cleanupPeriode;
            this.cleanupPeriode = this.cleanupPeriode.Clamp(suppressionTime.Multiply(100), TimeSpan.MaxValue);

            // TODO make it testable
            new Timer(_ => CleanUpSuppressors(AppTime.Now), null, this.cleanupPeriode, this.cleanupPeriode);
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
                foreach(var suppressor in suppressors)
                {
                    if(isDuplicate(message, suppressor.message))
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
            while(suppressors.Count > 0)
            {
                if(now > suppressors.Peek().suppressionEnd) suppressors.Dequeue();
                else break;
            }
        }

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

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

        public void Post<M>(in M message)
        {
            if(!IsSuppressed(message)) sourceHelper.Forward(message);
        }

        public Task PostAsync<M>(M message)
        {
            if(IsSuppressed(message)) return Task.CompletedTask;
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