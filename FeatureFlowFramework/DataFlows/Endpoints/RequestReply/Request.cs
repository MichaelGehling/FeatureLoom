namespace FeatureFlowFramework.DataFlows.RequestReply
{
    public interface IRequest : IMessageWrapper
    {
        bool TryGetMessage<T>(out T requestMessage);

        IReply CreateReply<T>(T replyMessage);

        long GetRequestId();
    }

    public class Request<REQ> : IRequest, IMessageWrapper<REQ>
    {
        private REQ message;
        private long requestId;

        public Request(REQ requestMessage, long requestId)
        {
            this.message = requestMessage;
            this.requestId = requestId;
        }

        public REQ TypedMessage => message;
        public object Message => message;

        public IReply CreateReply<T>(T replyMessage)
        {
            return new Reply<T>(replyMessage, requestId);
        }

        public long GetRequestId()
        {
            return requestId;
        }

        public bool TryGetMessage<T>(out T requestMessage)
        {
            if (message is T tMsg)
            {
                requestMessage = tMsg;
                return true;
            }
            else
            {
                requestMessage = default;
                return false;
            }
        }
    }
}