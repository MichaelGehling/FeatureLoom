using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    public class TopicFilter : IMessageSink<TopicMessage>, IMessageSource, IMessageFlowConnection
    {
        SourceValueHelper sourceHelper = new SourceValueHelper();
        string topic;

        public TopicFilter(string topic)
        {
            this.topic = topic;
        }

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        public Type ConsumedMessageType => typeof(TopicMessage);

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

        public void Post<M>(in M message)
        {
            if (message is TopicMessage topicMessage && topicMessage.topic == topic)
            {
                sourceHelper.Forward(in topicMessage.message);
            }
        }

        public void Post<M>(M message)
        {
            if (message is TopicMessage topicMessage && topicMessage.topic == topic)
            {
                sourceHelper.Forward(topicMessage.message);
            }
        }

        public Task PostAsync<M>(M message)
        {
            if (message is TopicMessage topicMessage && topicMessage.topic == topic)
            {
                return sourceHelper.ForwardAsync(message);
            }
            return Task.CompletedTask;
        }
    }

    public class TopicFilter<T> : IMessageSink<TopicMessage>, IMessageSource<T>, IMessageFlowConnection
    {
        TypedSourceValueHelper<T> sourceHelper = new TypedSourceValueHelper<T>();
        string topic;

        public TopicFilter(string topic)
        {
            this.topic = topic;
        }

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        public Type ConsumedMessageType => typeof(TopicMessage);

        public Type SentMessageType => typeof(T);

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

        public void Post<M>(in M message)
        {
            if (message is TopicMessage topicMessage && 
                topicMessage.topic == topic &&
                topicMessage.message is T typedMessage)
            {
                sourceHelper.Forward(in typedMessage);
            }
        }

        public void Post<M>(M message)
        {
            if (message is TopicMessage topicMessage &&
                topicMessage.topic == topic &&
                topicMessage.message is T typedMessage)
            {
                sourceHelper.Forward( typedMessage);
            }
        }

        public Task PostAsync<M>(M message)
        {
            if (message is TopicMessage topicMessage &&
                topicMessage.topic == topic &&
                topicMessage.message is T typedMessage)
            {
                return sourceHelper.ForwardAsync(typedMessage);
            }
            return Task.CompletedTask;
        }
    }
}
