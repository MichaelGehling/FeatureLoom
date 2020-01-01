using FeatureFlowFramework.DataFlows.Test;
using System.Collections.Generic;
using Xunit;

namespace FeatureFlowFramework.DataFlows
{
    public class AggregatorTests
    {
        private class FullNameAggrergationData : IAggregationData
        {
            public string firstName;
            public string lastName;

            public (bool ready, object msg, bool enumerate) TryCreateOutputMessage()
            {
                if (firstName != null && lastName != null)
                {
                    var result = (true, firstName + " " + lastName, false);
                    firstName = null;
                    lastName = null;
                    return result;
                }
                else return (false, null, false);
            }
        }

        private class VariantNameAggrergationData : IAggregationData
        {
            public string firstName;
            public string lastName;

            public (bool ready, object msg, bool enumerate) TryCreateOutputMessage()
            {
                if (firstName != null && lastName != null)
                {
                    List<string> multipleResultMessages = new List<string>();
                    multipleResultMessages.Add(firstName + " " + lastName);
                    multipleResultMessages.Add(firstName + "_" + lastName);

                    var result = (true, multipleResultMessages, true);
                    firstName = null;
                    lastName = null;
                    return result;
                }
                else return (false, null, false);
            }
        }

        [Fact]
        public void CanAggregateComplementMessagesToASingleMessage()
        {
            var sender = new Sender();
            var aggregator = new Aggregator<(string key, string val), FullNameAggrergationData>((msg, aggregation) =>
            {
                if (msg.key == "firstName") aggregation.firstName = msg.val;
                else if (msg.key == "lastName") aggregation.lastName = msg.val;
                else return false;

                return true;
            });
            var sink = new SingleMessageTestSink<string>();
            sender.ConnectTo(aggregator).ConnectTo(sink);

            sender.Send(("bla", "bla"));
            Assert.False(sink.received);
            sender.Send(("firstName", "Jim"));
            Assert.False(sink.received);
            sender.Send(("firstName", "John"));
            Assert.False(sink.received);
            sender.Send(("lastName", "Doe"));
            Assert.True(sink.received);
            Assert.Equal("John Doe", sink.receivedMessage);

            sink.Reset();

            sender.Send(("firstName", "Jane"));
            Assert.False(sink.received);
            sender.Send(("lastName", "Doe"));
            Assert.True(sink.received);
            Assert.Equal("Jane Doe", sink.receivedMessage);
        }

        [Fact]
        public void ForwardsUnusedMessagesToElse()
        {
            var sender = new Sender();
            var aggregator = new Aggregator<(string key, string val), FullNameAggrergationData>((msg, aggregation) =>
            {
                if (msg.key == "firstName") aggregation.firstName = msg.val;
                else if (msg.key == "lastName") aggregation.lastName = msg.val;
                else return false;

                return true;
            });
            var elseSink = new SingleMessageTestSink<object>();
            sender.ConnectTo(aggregator);
            aggregator.Else.ConnectTo(elseSink);

            sender.Send(("bla", "bla"));
            Assert.True(elseSink.received);
            Assert.Equal(("bla", "bla"), elseSink.receivedMessage);
        }

        [Fact]
        public void CanProduceMultipleMessagesAsAggregationResult()
        {
            var sender = new Sender();
            var aggregator = new Aggregator<(string key, string val), VariantNameAggrergationData>((msg, aggregation) =>
            {
                if (msg.key == "firstName") aggregation.firstName = msg.val;
                else if (msg.key == "lastName") aggregation.lastName = msg.val;
                else return false;

                return true;
            });
            var receiver = new QueueReceiver<string>();
            sender.ConnectTo(aggregator).ConnectTo(receiver);
            sender.Send(("firstName", "John"));
            Assert.True(receiver.Count == 0);
            sender.Send(("lastName", "Doe"));
            Assert.True(receiver.Count == 2);
            Assert.Equal("John Doe", receiver.TryReceive(out string name1) ? name1 : null);
            Assert.Equal("John_Doe", receiver.TryReceive(out string name2) ? name2 : null);
        }
    }
}