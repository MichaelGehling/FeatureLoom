using FeatureLoom.MessageFlow;
using FeatureLoom.Synchronization;
using FeatureLoom.Time;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    public sealed class DelayingForwarder : IMessageSink, IMessageSource, IMessageFlowConnection
    {
        private SourceValueHelper sourceHelper = new SourceValueHelper();
        private readonly TimeSpan minDelay;
        private readonly TimeSpan maxDelay;
        private bool blocking;

        public DelayingForwarder(TimeSpan minDelay, TimeSpan maxDelay = default, bool blocking = false)
        {
            this.minDelay = minDelay;
            this.maxDelay = maxDelay == default ? minDelay : maxDelay;
            this.blocking = blocking;
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
            Post(message);
        }

        public void Post<M>(M message)
        {
            if (blocking)
            {
                AppTime.Wait(minDelay, maxDelay);
                sourceHelper.Forward(message);
            }
            else
            {
                var tk = AppTime.TimeKeeper;
                Task.Run(() =>
                {
                    AppTime.Wait(minDelay - tk.Elapsed, maxDelay - tk.LastElapsed);
                    sourceHelper.Forward(message);
                });
            }
        }

        public async Task PostAsync<M>(M message)
        {
            if (blocking)
            {
                await AppTime.WaitAsync(minDelay, maxDelay).ConfiguredAwait();
                await sourceHelper.ForwardAsync(message).ConfiguredAwait();
            }
            else
            {
                var tk = AppTime.TimeKeeper;
                _ = Task.Run(async () =>
                {
                    await AppTime.WaitAsync(minDelay - tk.Elapsed, maxDelay - tk.LastElapsed).ConfiguredAwait();
                    await sourceHelper.ForwardAsync(message).ConfiguredAwait();
                });
            }            
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