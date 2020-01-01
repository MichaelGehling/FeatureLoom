namespace FeatureFlowFramework.DataFlows
{
    public interface IReply
    {
        bool TryGetMessage<T>(out T replyMessage);

        long GetRequestId();
    }

    public class Reply<REP> : IReply
    {
        private REP message;
        private long requestId;

        public Reply(REP replyMessage, long requestId)
        {
            this.message = replyMessage;
            this.requestId = requestId;
        }

        public long GetRequestId()
        {
            return requestId;
        }

        public bool TryGetMessage<T>(out T replyMessage)
        {
            if (message is T tMsg)
            {
                replyMessage = tMsg;
                return true;
            }
            else
            {
                replyMessage = default;
                return false;
            }
        }
    }
}