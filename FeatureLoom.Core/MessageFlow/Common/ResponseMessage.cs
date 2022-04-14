namespace FeatureLoom.MessageFlow
{
    public class ResponseMessage<T> : IResponseMessage<T>
    {
        T content;
        long requestId;

        public ResponseMessage(T content, long requestId)
        {
            this.content = content;
            this.requestId = requestId;
        }

        public T Content => content;

        public long RequestId { get => requestId; set => requestId = value; }

        public static implicit operator T(ResponseMessage<T> req) => req.content;

    }
}
