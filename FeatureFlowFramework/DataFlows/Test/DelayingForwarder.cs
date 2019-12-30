using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows.Test
{
    public class DelayingForwarder : IDataFlowSink, IDataFlowSource, IDataFlowConnection
    {
        DataFlowSourceHelper helper = new DataFlowSourceHelper();
        TimeSpan delay;

        public DelayingForwarder(TimeSpan delay)
        {
            this.delay = delay;
        }

        public int CountConnectedSinks => ((IDataFlowSource)helper).CountConnectedSinks;

        public void ConnectTo(IDataFlowSink sink)
        {
            ((IDataFlowSource)helper).ConnectTo(sink);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink)
        {
            return ((IDataFlowSource)helper).ConnectTo(sink);
        }

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
            Thread.Sleep(delay);
            await helper.ForwardAsync(message);
        }
    }
}
