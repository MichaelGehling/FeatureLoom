using FeatureLoom.MessageFlow;
using System.Threading.Tasks;

namespace FeatureLoom.TCP
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

        public void UnwrapAndSend(ISender sender)
        {
            sender.Send(message);
        }

        public void UnwrapAndSendByRef(ISender sender)
        {
            sender.Send(in message);
        }

        public Task UnwrapAndSendAsync(ISender sender)
        {
            return sender.SendAsync(message);
        }
    }
}