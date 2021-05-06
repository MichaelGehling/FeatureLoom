using FeatureLoom.DataFlows;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Diagnostics
{
    public class DelayingForwarder : IDataFlowSink, IDataFlowSource, IDataFlowConnection
    {
        private SourceValueHelper sourceHelper = new SourceValueHelper();
        private readonly TimeSpan delay;

        public DelayingForwarder(TimeSpan delay)
        {
            this.delay = delay;
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
            Thread.Sleep(delay);
            sourceHelper.Forward(message);
        }

        public async Task PostAsync<M>(M message)
        {
            await Task.Delay(delay);
            await sourceHelper.ForwardAsync(message);
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