using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureLoom.MessageFlow
{
    public readonly struct TopicMessage
    {
        readonly public string topic;
        readonly public object message;

        public TopicMessage(string topic, object message)
        {
            this.topic = topic;
            this.message = message;
        }
    }
}
