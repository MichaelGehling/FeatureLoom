using FeatureLoom.MessageFlow;
using FeatureLoom.Time;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Diagnostics
{
    public class DelayingForwarder : IMessageSink, IMessageSource, IMessageFlowConnection
    {
        private SourceValueHelper sourceHelper = new SourceValueHelper();
        private readonly TimeSpan delay;
        private bool blocking;

        public DelayingForwarder(TimeSpan delay, bool blocking = false)
        {
            this.delay = delay;
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
                AppTime.Wait(delay);
                sourceHelper.Forward(message);
            }
            else
            {
                Task.Run(() =>
                {
                    AppTime.Wait(delay);
                    sourceHelper.Forward(message);
                });
            }
        }

        public async Task PostAsync<M>(M message)
        {
            if (blocking)
            {
                await AppTime.WaitAsync(delay);
                await sourceHelper.ForwardAsync(message);
            }
            else
            {
                _ = Task.Run(async () =>
                {
                    await AppTime.WaitAsync(delay);
                    await sourceHelper.ForwardAsync(message);
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
    }
}