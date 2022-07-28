using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    public interface ITopicMessage
    {
        string Topic { get; }
        void ForwardMessage(IMessageSink sink);
        Task ForwardMessageAsync(IMessageSink sink);
        void ForwardMessageByRef(IMessageSink sink);
    }

    public readonly struct TopicMessage<T> : ITopicMessage
    {
        private readonly string topic;
        private readonly T message;

        public string Topic => topic;

        public T Message => message;

        public TopicMessage(string topic, T message)
        {
            this.topic = topic;
            this.message = message;
        }

        public void ForwardMessage(IMessageSink sink)
        {
            sink.Post(message);
        }

        public void ForwardMessageByRef(IMessageSink sink)
        {
            sink.Post(in message);
        }

        public Task ForwardMessageAsync(IMessageSink sink)
        {
            return sink.PostAsync(message);
        }
    }
}
