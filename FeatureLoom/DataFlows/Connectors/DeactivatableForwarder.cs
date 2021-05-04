using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FeatureLoom.DataFlows
{
    public class DeactivatableForwarder : IDataFlowSink, IDataFlowSource, IDataFlowConnection
    {
        SourceValueHelper sourceHelper;
        bool active = true;
        readonly Func<bool> autoActivationCondition = null;

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

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        public bool Active { get => active; set => active = value; }

        public void ConnectTo(IDataFlowSink sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink, bool weakReference = false)
        {
            return sourceHelper.ConnectTo(sink, weakReference);
        }

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

    }
}
