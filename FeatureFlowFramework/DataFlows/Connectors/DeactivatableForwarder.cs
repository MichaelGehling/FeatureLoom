using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows
{
    public class DeactivatableForwarder : IDataFlowSink, IDataFlowSource, IDataFlowConnection
    {
        DataFlowSourceHelper sourceHelper = new DataFlowSourceHelper();
        bool active = true;
        Func<bool> autoActivationCondition = null;

        public DeactivatableForwarder(Func<bool> autoActivationCondition = null)
        {
            this.autoActivationCondition = autoActivationCondition;
        }

        public void Post<M>(in M message)
        {
            if(autoActivationCondition != null) active = autoActivationCondition();
            if (active) sourceHelper.Forward(message);
        }

        public Task PostAsync<M>(M message)
        {
            if(autoActivationCondition != null) active = autoActivationCondition();
            if(active) return sourceHelper.ForwardAsync(message);
            else return Task.CompletedTask;
        }

        public int CountConnectedSinks => ((IDataFlowSource)sourceHelper).CountConnectedSinks;

        public bool Active { get => active; set => active = value; }

        public void ConnectTo(IDataFlowSink sink, bool weakReference = false)
        {
            ((IDataFlowSource)sourceHelper).ConnectTo(sink, weakReference);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink, bool weakReference = false)
        {
            return ((IDataFlowSource)sourceHelper).ConnectTo(sink, weakReference);
        }

        public void DisconnectAll()
        {
            ((IDataFlowSource)sourceHelper).DisconnectAll();
        }

        public void DisconnectFrom(IDataFlowSink sink)
        {
            ((IDataFlowSource)sourceHelper).DisconnectFrom(sink);
        }

        public IDataFlowSink[] GetConnectedSinks()
        {
            return ((IDataFlowSource)sourceHelper).GetConnectedSinks();
        }

    }
}
