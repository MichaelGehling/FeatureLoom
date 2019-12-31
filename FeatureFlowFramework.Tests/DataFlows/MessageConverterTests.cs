using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.DataFlows.Test;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace FeatureFlowFramework.DataFlows
{
    public class MessageConverterTests
    {
        [Fact]
        public void CanConvertMessage()
        {
            var sender = new Sender<string>();
            var converter = new MessageConverter<string, int>(str => int.Parse(str));
            var sink = new SingleMessageTestSink<int>();
            sender.ConnectTo(converter).ConnectTo(sink);
            sender.Send("42");
            Assert.True(sink.received);
            Assert.Equal(42, sink.receivedMessage);
        }
    }
}
