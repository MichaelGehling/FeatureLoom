using System;

namespace FeatureFlowFramework.DataFlows
{
    public interface IRequest
    {
        bool TryGetMessage<T>(out T requestMessage);

        IReply CreateReply<T>(T replyMessage);

        long GetRequestId();
    }

    public class Request<REQ> : IRequest
    {
        public readonly REQ message;
        public readonly long requestId;

        public Request(REQ requestMessage, long requestId)
        {
            this.message = requestMessage;
            this.requestId = requestId;
        }

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
            if(message is T tMsg)
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
