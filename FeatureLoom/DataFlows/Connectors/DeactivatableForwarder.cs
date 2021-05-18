using System;
using System.Threading.Tasks;

namespace FeatureLoom.DataFlows
{
    /// <summary>
    /// Forwarder that can be activated and deactivated. When deactivated it will not forward any message.
    /// Activation and deactivation can either be done via the Active property or it can be done
    /// automatically be providing a function delegate that checks each time a message is received if
    /// forwarder is active or not. This allows to inhibit communication in specific application states.
    /// </summary>
    public class DeactivatableForwarder : IDataFlowSink, IDataFlowSource, IDataFlowConnection
    {
        private SourceValueHelper sourceHelper;
        private bool active = true;
        private readonly Func<bool> autoActivationCondition = null;

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        public bool Active { get => active; set => active = value; }

        public DeactivatableForwarder(Func<bool> autoActivationCondition = null)
        {
            this.autoActivationCondition = autoActivationCondition;
        }

        public void Post<M>(in M message)
        {
            if (autoActivationCondition != null) active = autoActivationCondition();
            if (active) sourceHelper.Forward(in message);
        }

        public void Post<M>(M message)
        {
            if (autoActivationCondition != null) active = autoActivationCondition();
            if (active) sourceHelper.Forward(message);
        }

        public Task PostAsync<M>(M message)
        {
            if (autoActivationCondition != null) active = autoActivationCondition();
            if (active) return sourceHelper.ForwardAsync(message);
            else return Task.CompletedTask;
        }        

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