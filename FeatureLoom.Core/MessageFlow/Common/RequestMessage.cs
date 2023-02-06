using FeatureLoom.Helpers;
using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureLoom.MessageFlow
{
    public class RequestMessage<T> : IRequestMessage<T>
    {
        T content;
        long requestId;

        public RequestMessage(T content, long requestId)
        {
            this.content = content;
            this.requestId = requestId;
        }

        public RequestMessage(T content)
        {
            this.content = content;
            this.requestId = RandomGenerator.Int64();
        }

        public RequestMessage()
        {

        }

        public long RequestId { get => requestId; set => requestId = value; }

        public ResponseMessage<RESP> CreateResponse<RESP>(RESP content)
        {
            return new ResponseMessage<RESP>(content, requestId);
        }

        public T Content { get => content; set => content = value; }

        public static implicit operator T(RequestMessage<T> req) => req.content;        

    }
}
