namespace FeatureLoom.DataFlows.TCP
{
    public class ConnectionRoutingWrapper : IMessageWrapper
    {
        public readonly long connectionId;
        public bool inverseConnectionFiltering = false;
        private object message;

        public object Message { get => message; set => message = value; }

        public ConnectionRoutingWrapper(long connectionId, object message)
        {
            this.connectionId = connectionId;
            this.message = message;
        }

        public ConnectionRoutingWrapper(object message)
        {
            this.connectionId = default;
            this.message = message;
        }
    }
}