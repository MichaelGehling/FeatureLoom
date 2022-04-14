using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureLoom.MessageFlow
{
    public class RequestMessage<T> : IRequestMessage<T>
    {
        T content;
        long requestId;

        public RequestMessage(T content)
        {
            this.content = content;
        }

        public long RequestId { get => requestId; set => requestId = value; }

        public T Content => content;

        public static implicit operator T(RequestMessage<T> req) => req.content;        

    }
}
