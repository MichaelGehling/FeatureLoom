using System;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    /// <summary>
    /// Forwarder that can be activated and deactivated. When deactivated it will not forward any message.
    /// Activation and deactivation can either be done via the Active property or it can be done
    /// automatically be providing a function delegate that checks each time a message is received if
    /// forwarder is active or not. This allows to inhibit communication in specific application states.
    /// </summary>
    public sealed class DeactivatableForwarder : IMessageSink, IMessageSource, IMessageFlowConnection
    {
        private SourceValueHelper sourceHelper;
        private bool active = true;
        private readonly Func<bool> autoActivationCondition = null;

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        /// <summary> Indicates whether there are no connected sinks. </summary>
        public bool NoConnectedSinks => sourceHelper.NotConnected;

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

        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
        }

        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
        {
            return sourceHelper.ConnectTo(sink, weakReference);
        }

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

        public bool IsConnected(IMessageSink sink)
        {
            return sourceHelper.IsConnected(sink);
        }
    }
}