using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows.Test
{
    public class DelayingForwarder : IDataFlowSink, IDataFlowSource, IDataFlowConnection
    {
        private DataFlowSourceHelper helper = new DataFlowSourceHelper();
        private readonly TimeSpan delay;

        public DelayingForwarder(TimeSpan delay)
        {
            this.delay = delay;
        }

        public int CountConnectedSinks => ((IDataFlowSource)helper).CountConnectedSinks;

        public void DisconnectAll()
        {
            ((IDataFlowSource)helper).DisconnectAll();
        }

        public void DisconnectFrom(IDataFlowSink sink)
        {
            ((IDataFlowSource)helper).DisconnectFrom(sink);
        }

        public IDataFlowSink[] GetConnectedSinks()
        {
            return ((IDataFlowSource)helper).GetConnectedSinks();
        }

        public void Post<M>(in M message)
        {
            Thread.Sleep(delay);
            helper.Forward(message);
        }

        public async Task PostAsync<M>(M message)
        {
            await Task.Delay(delay);
            await helper.ForwardAsync(message);
        }

        public void ConnectTo(IDataFlowSink sink, bool weakReference = false)
        {
            ((IDataFlowSource)helper).ConnectTo(sink, weakReference);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink, bool weakReference = false)
        {
            return ((IDataFlowSource)helper).ConnectTo(sink, weakReference);
        }
    }
}