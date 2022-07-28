using FeatureLoom.Extensions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    public class TopicFilter : IMessageSink, IMessageSource, IMessageFlowConnection
    {
        Forwarder forwarder = new Forwarder();
        string topic;
        bool includesWildcards;

        public TopicFilter(string topic)
        {
            this.topic = topic;
            this.includesWildcards = topic.Contains('*') || topic.Contains('?');
        }

        public int CountConnectedSinks => forwarder.CountConnectedSinks;

        public void ConnectTo(IMessageSink sink, bool weakReference = false)
        {
            forwarder.ConnectTo(sink, weakReference);
        }

        public IMessageSource ConnectTo(IMessageFlowConnection sink, bool weakReference = false)
        {
            return forwarder.ConnectTo(sink, weakReference);
        }

        public void DisconnectAll()
        {
            forwarder.DisconnectAll();
        }

        public void DisconnectFrom(IMessageSink sink)
        {
            forwarder.DisconnectFrom(sink);
        }

        public IMessageSink[] GetConnectedSinks()
        {
            return forwarder.GetConnectedSinks();
        }

        public void Post<M>(in M message)
        {
            if (message is ITopicMessage topicMessage)
            {
                if (includesWildcards)
                {
                    if (topicMessage.Topic.MatchesWildcard(topic)) topicMessage.ForwardMessageByRef(forwarder);
                }
                else if (topicMessage.Topic == topic) topicMessage.ForwardMessageByRef(forwarder);
            }
        }

        public void Post<M>(M message)
        {
            if (message is ITopicMessage topicMessage)
            {
                if (includesWildcards)
                {
                    if (topicMessage.Topic.MatchesWildcard(topic)) topicMessage.ForwardMessage(forwarder);
                }
                else if (topicMessage.Topic == topic) topicMessage.ForwardMessage(forwarder);
            }
        }

        public Task PostAsync<M>(M message)
        {
            if (message is ITopicMessage topicMessage)
            {
                if (includesWildcards)
                {
                    if (topicMessage.Topic.MatchesWildcard(topic)) return topicMessage.ForwardMessageAsync(forwarder);
                }
                else if (topicMessage.Topic == topic) return topicMessage.ForwardMessageAsync(forwarder);
            }
            return Task.CompletedTask;
        }
    }

    public class TopicFilter<T> : IMessageSink<TopicMessage<T>>, IMessageSource<T>, IMessageFlowConnection
    {
        TypedSourceValueHelper<T> sourceHelper = new TypedSourceValueHelper<T>();
        string topic;
        bool includesWildcards;

        public TopicFilter(string topic)
        {
            this.topic = topic;
            this.includesWildcards = topic.Contains('*') || topic.Contains('?');
        }

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        public Type ConsumedMessageType => typeof(TopicMessage<T>);

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
            if (message is TopicMessage<T> topicMessage)
            {
                T typedMesage = topicMessage.Message;
                if (includesWildcards)
                {
                    if (topicMessage.Topic.MatchesWildcard(topic)) sourceHelper.Forward(in typedMesage);
                }
                else if (topicMessage.Topic == topic) sourceHelper.Forward(in typedMesage);
            }
        }

        public void Post<M>(M message)
        {
            if (message is TopicMessage<T> topicMessage)
            {                
                if (includesWildcards)
                {
                    if (topicMessage.Topic.MatchesWildcard(topic)) sourceHelper.Forward(topicMessage.Message);
                }
                else if (topicMessage.Topic == topic) sourceHelper.Forward(topicMessage.Message);
            }
        }

        public Task PostAsync<M>(M message)
        {
            if (message is TopicMessage<T> topicMessage)
            {
                if (includesWildcards)
                {
                    if (topicMessage.Topic.MatchesWildcard(topic)) return sourceHelper.ForwardAsync(topicMessage.Message);
                }
                else if (topicMessage.Topic == topic) return sourceHelper.ForwardAsync(topicMessage.Message);
            }
            return Task.CompletedTask;
        }
    }
}
