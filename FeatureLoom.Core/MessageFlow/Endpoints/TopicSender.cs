using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    /// <summary> Used to send messages of any type wrapped as topic messages to all connected sinks. It is thread safe. <summary>
    public sealed class TopicSender : IMessageSource, ISender
    {
        SourceValueHelper sourceHelper;
        string topic;

        public TopicSender(string topic)
        {
            this.topic = topic;
        }

        public void Send<T>(in T message)
        {
            var topicMessage = new TopicMessage<T>(topic, message);
            sourceHelper.Forward(in topicMessage);
        }

        public void Send<T>(T message)
        {
            var topicMessage = new TopicMessage<T>(topic, message);
            sourceHelper.Forward(topicMessage);
        }

        public Task SendAsync<T>(T message)
        {
            var topicMessage = new TopicMessage<T>(topic, message);
            return sourceHelper.ForwardAsync(topicMessage);
        }

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

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

        public bool IsConnected(IMessageSink sink)
        {
            return sourceHelper.IsConnected(sink);
        }
    }

    /// <summary> Used to send messages of a specific type wrapped as topic messages to all connected sinks. It is thread safe. <summary>
    public sealed class TopicSender<T> : ISender<T>, IMessageSource<TopicMessage<T>>
    {
        TypedSourceValueHelper<TopicMessage<T>> sourceHelper;
        string topic;

        public TopicSender(string topic)
        {
            this.topic = topic;
        }

        public Type SentMessageType => sourceHelper.SentMessageType;

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

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

        public bool IsConnected(IMessageSink sink)
        {
            return sourceHelper.IsConnected(sink);
        }

        public void Send(in T message)
        {
            var topicMessage = new TopicMessage<T>(topic, message);
            sourceHelper.Forward(in topicMessage);
        }

        public void Send(T message)
        {
            var topicMessage = new TopicMessage<T>(topic, message);
            sourceHelper.Forward(topicMessage);
        }

        public Task SendAsync(T message)
        {
            var topicMessage = new TopicMessage<T>(topic, message);
            return sourceHelper.ForwardAsync(topicMessage);
        }

    }
}
